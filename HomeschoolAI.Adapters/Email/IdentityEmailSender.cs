using Microsoft.AspNetCore.Identity;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Domain.Email;

namespace HomeschoolAI.Adapters.Email;

/// <summary>
/// Puente entre ASP.NET Core Identity y el <see cref="IEmailSender"/> genérico.
/// Implementa <c>IEmailSender&lt;FamilyMember&gt;</c> para la confirmación de cuenta
/// y la recuperación de contraseña.
/// </summary>
public sealed class IdentityEmailSender : IEmailSender<FamilyMember>
{
    private readonly IEmailSender _emailSender;

    public IdentityEmailSender(IEmailSender emailSender) => _emailSender = emailSender;

    public Task SendConfirmationLinkAsync(FamilyMember user, string email, string confirmationLink) =>
        _emailSender.SendAsync(email, "Confirma tu cuenta de HomeschoolAI",
            $"<p>Bienvenido. Confirma tu cuenta haciendo clic: <a href=\"{confirmationLink}\">{confirmationLink}</a></p>");

    public Task SendPasswordResetLinkAsync(FamilyMember user, string email, string resetLink) =>
        _emailSender.SendAsync(email, "Restablece tu contraseña",
            $"<p>Restablece tu contraseña: <a href=\"{resetLink}\">{resetLink}</a></p>");

    public Task SendPasswordResetCodeAsync(FamilyMember user, string email, string resetCode) =>
        _emailSender.SendAsync(email, "Código de restablecimiento",
            $"<p>Tu código de restablecimiento es: <strong>{resetCode}</strong></p>");
}
