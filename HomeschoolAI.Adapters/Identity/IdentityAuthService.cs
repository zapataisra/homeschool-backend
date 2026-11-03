using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Application.Auth;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Auth;
using HomeschoolAI.Domain.Exceptions;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Implementación de <see cref="IAuthService"/> con ASP.NET Core Identity
/// (UserManager, SignInManager) y cookies. Vive en Adapters porque Application
/// no puede referenciar ASP.NET.
/// </summary>
public sealed partial class IdentityAuthService : IAuthService
{
    private readonly UserManager<FamilyMember> _userManager;
    private readonly SignInManager<FamilyMember> _signInManager;
    private readonly AppDbContext _db;
    private readonly IUserContextProvider _userContextProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<IdentityAuthService> _logger;

    public IdentityAuthService(
        UserManager<FamilyMember> userManager,
        SignInManager<FamilyMember> signInManager,
        AppDbContext db,
        IUserContextProvider userContextProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<IdentityAuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _userContextProvider = userContextProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var familyName = request.FamilyName.Trim();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ValidationException("El correo no es válido.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ValidationException("La contraseña debe tener al menos 8 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(familyName))
        {
            throw new ValidationException("El nombre de la familia es obligatorio.");
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new EmailAlreadyRegisteredException();
        }

        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = familyName,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var member = new FamilyMember
        {
            UserName = email,
            Email = email,
            FamilyId = family.Id,
            Role = FamilyRole.Owner
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Families.Add(family);

            var result = await _userManager.CreateAsync(member, request.Password);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                throw new ValidationException($"Registro inválido: {errors}");
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await _signInManager.SignInAsync(member, isPersistent: false);

        LogRegistered(_logger, family.Id, email.Length);

        return new AuthResponse(member.Id, family.Id, nameof(UserRole.Owner));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Correo y contraseña son obligatorios.");
        }

        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new InvalidCredentialsException();

        var result = await _signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            LogLoggedIn(_logger, user.FamilyId, email.Length);
            return new AuthResponse(user.Id, user.FamilyId, user.Role.ToString());
        }

        if (result.IsLockedOut)
        {
            throw new ForbiddenException("Cuenta bloqueada temporalmente por intentos fallidos. Intenta más tarde.");
        }

        throw new InvalidCredentialsException();
    }
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<ChildSessionResponse> StartChildSessionAsync(Guid childId, CancellationToken cancellationToken = default)
    {
        var user = await _userContextProvider.GetAsync(cancellationToken)
            ?? throw new InvalidCredentialsException();

        if (user.Role is not (UserRole.Owner or UserRole.Guardian))
        {
            throw new ForbiddenException("Solo un adulto puede iniciar la sesión de un niño.");
        }

        var child = await _db.ChildProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == childId && c.FamilyId == user.FamilyId, cancellationToken)
            ?? throw new ResourceNotFoundException("ChildProfile");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.FamilyMemberId),
            new(ClaimTypes.Name, child.Name),
            new(ClaimNames.FamilyId, user.FamilyId.ToString()),
            new(ClaimNames.Role, nameof(UserRole.Child)),
            new(ClaimNames.ChildProfileId, child.Id.ToString()),
            new(ClaimNames.ChildName, child.Name)
        };

        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(identity);

        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidCredentialsException();

        await httpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });

        LogChildSessionStarted(_logger, user.FamilyId, child.Id, child.Name.Length);

        return new ChildSessionResponse(child.Id, child.Name);
    }

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information,
        Message = "Usuario registrado: familia {FamilyId}, longitud de correo {EmailLength}")]
    private static partial void LogRegistered(ILogger logger, Guid familyId, int emailLength);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Information,
        Message = "Inicio de sesión correcto: familia {FamilyId}, longitud de correo {EmailLength}")]
    private static partial void LogLoggedIn(ILogger logger, Guid familyId, int emailLength);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information,
        Message = "Sesión de niño iniciada: familia {FamilyId}, niño {ChildId}, longitud de nombre {NameLength}")]
    private static partial void LogChildSessionStarted(ILogger logger, Guid familyId, Guid childId, int nameLength);
}

