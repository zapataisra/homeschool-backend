using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Domain.Email;

namespace HomeschoolAI.Adapters.Email;

/// <summary>
/// Registro del servicio de correo (adaptador SMTP local → Mailpit).
/// </summary>
public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddHomeschoolEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection("Email"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Email:Host es obligatorio.")
            .Validate(options => options.Port is > 0 and < 65536, "Email:Port debe ser un puerto válido.");

        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
