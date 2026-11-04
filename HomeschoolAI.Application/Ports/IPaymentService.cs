namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de entrada de pagos (billing). La implementación usa Stripe en modo de
/// prueba. El servidor es la fuente de verdad del pago: solo un webhook verificado
/// activa derechos; la redirección tras pagar no activa nada.
/// </summary>
public interface IPaymentService
{
    /// <summary>Crea una sesión de Stripe Checkout para la suscripción mensual y devuelve su URL.</summary>
    Task<string> CreateCheckoutSessionAsync(Guid familyId, CancellationToken cancellationToken = default);

    /// <summary>Verifica la firma de un webhook de Stripe y procesa el evento (activa IsPremium).</summary>
    Task ProcessWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);
}
