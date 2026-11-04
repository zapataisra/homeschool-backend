namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de entrada de la cuota freemium: consume un crédito gratuito si la familia
/// no es premium, o lanza <see cref="Domain.Exceptions.PaymentRequiredException"/> (402)
/// cuando se agotaron. Las familias premium no consumen créditos.
/// </summary>
public interface IInteractionQuotaService
{
    Task ConsumeInteractionAsync(Guid familyId, CancellationToken cancellationToken = default);
}
