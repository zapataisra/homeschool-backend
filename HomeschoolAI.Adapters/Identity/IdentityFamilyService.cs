using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Application.Auth;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Auth;
using HomeschoolAI.Domain.Exceptions;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Implementación de <see cref="IFamilyService"/>: accede a los datos de la familia
/// siempre filtrando por el <c>FamilyId</c> del usuario autenticado (aislamiento IDOR).
/// </summary>
public sealed class IdentityFamilyService : IFamilyService
{
    private readonly AppDbContext _db;
    private readonly IUserContextProvider _userContextProvider;
    private readonly ILogger<IdentityFamilyService> _logger;

    public IdentityFamilyService(
        AppDbContext db,
        IUserContextProvider userContextProvider,
        ILogger<IdentityFamilyService> logger)
    {
        _db = db;
        _userContextProvider = userContextProvider;
        _logger = logger;
    }

    public async Task<FamilyInfoResponse> GetFamilyAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);

        var family = await _db.Families
            .AsNoTracking()
            .Where(f => f.Id == user.FamilyId)
            .Select(f => new FamilyInfoResponse(f.Id, f.Name, f.Members.Count, f.Children.Count))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ResourceNotFoundException("Family");

        return family;
    }

    public async Task<IReadOnlyList<ChildProfileDto>> GetChildrenAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);

        return await _db.ChildProfiles
            .AsNoTracking()
            .Where(c => c.FamilyId == user.FamilyId)
            .OrderBy(c => c.Name)
            .Select(c => new ChildProfileDto(c.Id, c.Name, c.Grade, c.Fase))
            .ToListAsync(cancellationToken);
    }

    public async Task<ChildProfileDto> CreateChildAsync(CreateChildRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("El nombre del niño es obligatorio.");
        }

        if (request.Grade is < 1 or > 6)
        {
            throw new ValidationException("El grado debe estar entre 1 y 6.");
        }

        var child = new ChildProfile
        {
            Id = Guid.NewGuid(),
            FamilyId = user.FamilyId,
            Name = name,
            Grade = request.Grade,
            Fase = request.Fase,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.ChildProfiles.Add(child);
        await _db.SaveChangesAsync(cancellationToken);

        return new ChildProfileDto(child.Id, child.Name, child.Grade, child.Fase);
    }

    public async Task<ChildProfileDto> GetCurrentChildAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);

        if (user.Role != UserRole.Child || user.ChildProfileId is null)
        {
            throw new ForbiddenException("Solo disponible en la sesión de un niño.");
        }

        var child = await _db.ChildProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == user.ChildProfileId.Value && c.FamilyId == user.FamilyId, cancellationToken)
            ?? throw new ResourceNotFoundException("ChildProfile");

        return new ChildProfileDto(child.Id, child.Name, child.Grade, child.Fase);
    }

    public async Task<BillingResponse> GetBillingAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);

        if (user.Role != UserRole.Owner)
        {
            throw new ForbiddenException("Solo el Owner puede ver la facturación.");
        }

        // Placeholder: la facturación real se implementa en la semana de pagos.
        return new BillingResponse(user.FamilyId, "activa");
    }

    private async Task<UserContext> RequireUserAsync(CancellationToken cancellationToken)
        => await _userContextProvider.GetAsync(cancellationToken) ?? throw new InvalidCredentialsException();
}
