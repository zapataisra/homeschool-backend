using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Exceptions;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Cuota freemium: las familias gratuitas tienen un número finito de interacciones;
/// las premium no tienen límite. Se consume un crédito ANTES de llamar al LLM para no
/// incurrir en coste de IA cuando ya no quedan interacciones.
/// </summary>
public sealed class InteractionQuotaService : IInteractionQuotaService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InteractionQuotaService> _logger;

    public InteractionQuotaService(AppDbContext db, ILogger<InteractionQuotaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ConsumeInteractionAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        var family = await _db.Families
            .SingleOrDefaultAsync(f => f.Id == familyId, cancellationToken)
            ?? throw new ResourceNotFoundException("Family");

        if (family.IsPremium)
        {
            return;
        }

        if (family.FreeInteractionsLeft <= 0)
        {
            throw new PaymentRequiredException(
                "Has agotado las interacciones gratuitas. Suscríbete para continuar.");
        }

        family.FreeInteractionsLeft -= 1;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Interacción gratuita consumida: FamilyId={FamilyId}, restantes={FreeInteractionsLeft}.",
            familyId, family.FreeInteractionsLeft);
    }
}
