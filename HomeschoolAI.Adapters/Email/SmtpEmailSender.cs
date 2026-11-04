using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HomeschoolAI.Domain.Email;

namespace HomeschoolAI.Adapters.Email;

/// <summary>
/// Envío de correo vía SMTP (Mailpit local). Usa <see cref="SmtpClient"/> de .NET:
/// no añade dependencias; para producción se recomienda MailKit.
/// </summary>
public sealed partial class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port);
        using var message = new MailMessage(_options.From, to, subject, htmlBody)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, cancellationToken);

        LogEmailSent(_logger, to.Length, subject.Length);
    }

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information,
        Message = "Correo enviado: destinatario de longitud {ToLength}, asunto de longitud {SubjectLength}")]
    private static partial void LogEmailSent(ILogger logger, int toLength, int subjectLength);
}
