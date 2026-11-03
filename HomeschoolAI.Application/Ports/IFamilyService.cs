using HomeschoolAI.Application.Auth;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de entrada: acceso a los datos de la familia, siempre filtrado por el
/// <c>FamilyId</c> del usuario autenticado (aislamiento entre familias).
/// </summary>
public interface IFamilyService
{
    Task<FamilyInfoResponse> GetFamilyAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChildProfileDto>> GetChildrenAsync(CancellationToken cancellationToken = default);

    Task<ChildProfileDto> CreateChildAsync(CreateChildRequest request, CancellationToken cancellationToken = default);

    /// <summary>Perfil del niño en la sesión delegada (rol Child).</summary>
    Task<ChildProfileDto> GetCurrentChildAsync(CancellationToken cancellationToken = default);

    /// <summary>Placeholder de facturación (solo Owner).</summary>
    Task<BillingResponse> GetBillingAsync(CancellationToken cancellationToken = default);
}
