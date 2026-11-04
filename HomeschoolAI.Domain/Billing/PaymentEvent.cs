namespace HomeschoolAI.Domain.Billing;

/// <summary>
/// Registro de cada evento de Stripe procesado por el webhook. La unicidad de
/// <see cref="StripeEventId"/> permite ignorar reintentos duplicados (idempotencia)
/// y auditar qué eventos llegaron.
/// </summary>
public sealed class PaymentEvent
{
    public Guid Id { get; set; }

    /// <summary>Identificador del evento en Stripe (evt_...). Único.</summary>
    public string StripeEventId { get; set; } = string.Empty;

    /// <summary>Tipo de evento (ej. checkout.session.completed).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Resultado del procesamiento (processed, duplicate...).</summary>
    public string Result { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
