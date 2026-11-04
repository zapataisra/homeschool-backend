using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Application.Ports;

namespace HomeschoolAI.Adapters.Payments;

/// <summary>Registro del adaptador de pagos (Stripe en modo de prueba).</summary>
public static class PaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "Stripe:SecretKey es obligatorio.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.WebhookSecret), "Stripe:WebhookSecret es obligatorio.");

        services.AddScoped<IPaymentService, StripePaymentService>();

        return services;
    }
}
