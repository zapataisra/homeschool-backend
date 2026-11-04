namespace HomeschoolAI.Domain.Email;

/// <summary>
/// Puerto de salida: envío de correo transaccional (confirmación de cuenta, recuperación).
/// La implementación local usa SMTP (Mailpit en desarrollo).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
