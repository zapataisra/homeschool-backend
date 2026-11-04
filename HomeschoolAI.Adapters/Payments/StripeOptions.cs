namespace HomeschoolAI.Adapters.Payments;

/// <summary>
/// Configuración de Stripe (modo de prueba). Los secretos llegan por variables de
/// entorno (<c>Stripe__SecretKey</c>, <c>Stripe__WebhookSecret</c>); nunca se versionan.
/// </summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Clave secreta de la API de Stripe (<c>sk_test_...</c> en desarrollo).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Secreto de firma del endpoint de webhook (<c>whsec_...</c>).</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>ID del Price de Stripe para la suscripción mensual (se crea en el dashboard).</summary>
    public string PriceId { get; set; } = string.Empty;

    public string SuccessUrl { get; set; } = "http://localhost:5173/pagos/exito";

    public string CancelUrl { get; set; } = "http://localhost:5173/pagos/cancelado";
}
