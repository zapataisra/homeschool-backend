using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Adapters.Payments;
using HomeschoolAI.Application.Auth;
using Xunit;

namespace HomeschoolAI.Tests;

[Collection(IntegrationCollection.Name)]
public class PaymentWebhookTests
{
    private readonly IdentityTestContext _ctx;

    public PaymentWebhookTests(IdentityTestContext ctx) => _ctx = ctx;

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static string BuildCheckoutCompletedPayload(Guid familyId) =>
        JsonSerializer.Serialize(new
        {
            id = $"evt_test_{Guid.NewGuid():N}",
            @object = "event",
            type = "checkout.session.completed",
            data = new
            {
                @object = new
                {
                    id = $"cs_test_{Guid.NewGuid():N}",
                    @object = "checkout.session",
                    metadata = new Dictionary<string, string>
                    {
                        [StripePaymentService.FamilyIdMetadataKey] = familyId.ToString()
                    }
                }
            }
        });

    private async Task<Guid> RegisterFamilyAsync(HttpClient client)
    {
        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Pago"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.FamilyId;
    }

    [Fact]
    public async Task Webhook_rejects_invalid_signature_with_400()
    {
        using var client = _ctx.CreateClient();
        var familyId = await RegisterFamilyAsync(client);

        var payload = BuildCheckoutCompletedPayload(familyId);
        // Firma bien formada pero firmada con un secreto DISTINTO → debe rechazarse.
        var wrongSignature = EventUtility.GenerateSignatureHeader(payload, "whsec_wrong_secret");
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", wrongSignature);

        var response = await client.PostAsync("/api/payments/webhook", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertFamilyPremiumAsync(familyId, premium: false);
    }

    [Fact]
    public async Task Webhook_marks_family_premium_on_valid_signature()
    {
        using var client = _ctx.CreateClient();
        var familyId = await RegisterFamilyAsync(client);

        var payload = BuildCheckoutCompletedPayload(familyId);
        // Firma válida calculada con el MISMO secreto que inyecta el host de pruebas.
        var signature = EventUtility.GenerateSignatureHeader(payload, IdentityTestContext.TestStripeWebhookSecret);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", signature);

        var response = await client.PostAsync("/api/payments/webhook", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertFamilyPremiumAsync(familyId, premium: true);
    }

    [Fact]
    public async Task Checkout_endpoint_requires_owner()
    {
        using var client = _ctx.CreateClient();

        // Anónimo → 401.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/payments/checkout", null)).StatusCode);

        // Owner se registra y crea un Guardian; el Guardian no puede facturar (403).
        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Pago 2"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();

        var guardianEmail = $"guardian_{Guid.NewGuid():N}@test.com";
        using (var scope = _ctx.Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FamilyMember>>();
            var guardian = new FamilyMember
            {
                UserName = guardianEmail,
                Email = guardianEmail,
                FamilyId = auth!.FamilyId,
                Role = FamilyRole.Guardian
            };
            var result = await userManager.CreateAsync(guardian, "Password1!");
            Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        using var guardianClient = _ctx.CreateClient();
        var login = await guardianClient.PostAsync("/api/auth/login", Json(new { email = guardianEmail, password = "Password1!" }));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await guardianClient.PostAsync("/api/payments/checkout", null)).StatusCode);
    }

    private async Task AssertFamilyPremiumAsync(Guid familyId, bool premium)
    {
        using var scope = _ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var family = await db.Families.SingleAsync(f => f.Id == familyId);
        Assert.Equal(premium, family.IsPremium);
    }
}
