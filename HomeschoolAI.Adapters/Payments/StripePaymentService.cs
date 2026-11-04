using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Stripe;
using Stripe.Checkout;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Billing;
using HomeschoolAI.Domain.Exceptions;

namespace HomeschoolAI.Adapters.Payments;

/// <summary>
/// Adaptador de pagos con Stripe (modo de prueba). El checkout genera una sesión de
/// suscripción mensual con el FamilyId como metadata; el webhook valida la firma
/// criptográfica ANTES de tocar la base de datos (nunca se confía en el cuerpo).
/// </summary>
public sealed class StripePaymentService : IPaymentService
{
    /// <summary>Clave de metadata que vincula la sesión de checkout con la familia (auditoría e idempotencia).</summary>
    public const string FamilyIdMetadataKey = "familyId";

    /// <summary>Tolerancia de antigüedad del timestamp del webhook (protección anti-replay).</summary>
    private const int SignatureToleranceSeconds = 300;

    /// <summary>Tipo de evento de Stripe que activa la suscripción premium.</summary>
    private const string CheckoutSessionCompletedEvent = "checkout.session.completed";

    private readonly StripeOptions _options;
    private readonly AppDbContext _db;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        IOptions<StripeOptions> options,
        AppDbContext db,
        ILogger<StripePaymentService> logger)
    {
        _options = options.Value;
        _db = db;
        _logger = logger;
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = _options.SuccessUrl,
            CancelUrl = _options.CancelUrl,
            Metadata = new Dictionary<string, string> { [FamilyIdMetadataKey] = familyId.ToString() },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions { Price = _options.PriceId, Quantity = 1 }
            }
        };

        var sessionService = new SessionService(new StripeClient(_options.SecretKey));
        var session = await sessionService.CreateAsync(options, cancellationToken: cancellationToken);

        return session.Url;
    }

    public async Task ProcessWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
    {
        Event stripeEvent;
        try
        {
            // Stripe firma con HMAC-SHA256 sobre "{timestamp}.{payload}". Si no coincide,
            // ConstructEvent lanza StripeException (firma falsa o manipulada).
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _options.WebhookSecret,
                tolerance: SignatureToleranceSeconds,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Firma del webhook de Stripe inválida: {Message}", ex.Message);
            throw new ValidationException("Firma del webhook inválida.");
        }

        if (stripeEvent.Type != CheckoutSessionCompletedEvent)
        {
            return;
        }

        var session = stripeEvent.Data.Object as Session;
        if (session?.Metadata is null
            || !session.Metadata.TryGetValue(FamilyIdMetadataKey, out var familyIdRaw)
            || !Guid.TryParse(familyIdRaw, out var familyId))
        {
            _logger.LogWarning("Webhook de checkout sin familyId válido en metadata.");
            return;
        }

        // Idempotencia: si el evento ya se procesó (reintento de Stripe), se ignora.
        if (await _db.PaymentEvents.AnyAsync(e => e.StripeEventId == stripeEvent.Id, cancellationToken))
        {
            _logger.LogInformation("Evento de Stripe ya procesado (idempotencia): {EventId}.", stripeEvent.Id);
            return;
        }

        var family = await _db.Families.SingleOrDefaultAsync(f => f.Id == familyId, cancellationToken);
        if (family is null)
        {
            _logger.LogWarning("Webhook de checkout para una familia inexistente: {FamilyId}.", familyId);
            return;
        }

        family.IsPremium = true;
        _db.PaymentEvents.Add(new PaymentEvent
        {
            Id = Guid.NewGuid(),
            StripeEventId = stripeEvent.Id,
            Type = stripeEvent.Type,
            Result = "processed",
            ReceivedAtUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Una petición concurrente ya registró este evento: idempotente.
            _logger.LogInformation("Evento de Stripe duplicado (carrera): {EventId}.", stripeEvent.Id);
            return;
        }

        _logger.LogInformation("Suscripción premium activada para la familia {FamilyId}.", familyId);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
