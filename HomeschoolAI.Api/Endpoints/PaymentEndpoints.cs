using Microsoft.AspNetCore.Authorization;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Auth;
using HomeschoolAI.Domain.Exceptions;

namespace HomeschoolAI.Api.Endpoints;

/// <summary>
/// Endpoints de pagos. El checkout exige rol Owner; el webhook es público, pero su
/// autorización es la firma criptográfica de Stripe (nunca una sesión de usuario).
/// </summary>
public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/payments/checkout", async (
                IPaymentService paymentService,
                IUserContextProvider userContextProvider,
                CancellationToken cancellationToken) =>
            {
                var user = await userContextProvider.GetAsync(cancellationToken)
                    ?? throw new ForbiddenException("No hay sesión de Owner activa.");

                var url = await paymentService.CreateCheckoutSessionAsync(user.FamilyId, cancellationToken);
                return Results.Ok(new { url });
            })
            .WithName("CreateCheckoutSession")
            .RequireAuthorization(AuthorizationPolicies.RequireOwner);

        endpoints.MapPost("/api/payments/webhook", async (
                HttpContext httpContext,
                IPaymentService paymentService,
                CancellationToken cancellationToken) =>
            {
                // Se lee el cuerpo RAW: la firma se calcula sobre esos bytes exactos,
                // por eso no se usa binding ni re-serialización del JSON.
                using var reader = new StreamReader(httpContext.Request.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signatureHeader = httpContext.Request.Headers["Stripe-Signature"].ToString();

                // Si la firma es inválida, ProcessWebhookAsync lanza ValidationException (400).
                await paymentService.ProcessWebhookAsync(payload, signatureHeader, cancellationToken);
                return Results.Ok();
            })
            .WithName("StripeWebhook")
            .AllowAnonymous();
    }
}
