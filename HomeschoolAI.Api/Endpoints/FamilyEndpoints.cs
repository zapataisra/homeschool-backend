using Microsoft.AspNetCore.Authorization;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Auth;
using HomeschoolAI.Application.Ports;

namespace HomeschoolAI.Api.Endpoints;

/// <summary>
/// Endpoints de la familia, protegidos por política de rol y filtrados por FamilyId.
/// </summary>
public static class FamilyEndpoints
{
    public static void MapFamilyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/family", async (IFamilyService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetFamilyAsync(cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.RequireFamilyMember)
            .WithName("GetFamily");

        endpoints.MapGet("/api/family/children", async (IFamilyService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetChildrenAsync(cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.RequireFamilyMember)
            .Produces<IReadOnlyList<ChildProfileDto>>(StatusCodes.Status200OK)
            .WithName("GetChildren");

        endpoints.MapPost("/api/family/children", async (
                CreateChildRequest request,
                IFamilyService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.CreateChildAsync(request, cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.RequireFamilyMember)
            .WithName("CreateChild");

        endpoints.MapPost("/api/family/children/{childId:guid}/session", async (
                Guid childId,
                IAuthService authService,
                CancellationToken cancellationToken) =>
                Results.Ok(await authService.StartChildSessionAsync(childId, cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.RequireFamilyMember)
            .WithName("StartChildSession");

        endpoints.MapGet("/api/family/me", async (IFamilyService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetCurrentChildAsync(cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.RequireChild)
            .WithName("GetCurrentChild");

        endpoints.MapGet("/api/family/billing", async (IFamilyService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetBillingAsync(cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.RequireOwner)
            .WithName("GetBilling");
    }
}
