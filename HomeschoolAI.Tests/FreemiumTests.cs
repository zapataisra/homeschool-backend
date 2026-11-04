using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Auth;
using Xunit;

namespace HomeschoolAI.Tests;

[Collection(IntegrationCollection.Name)]
public class FreemiumTests
{
    private readonly IdentityTestContext _ctx;

    public FreemiumTests(IdentityTestContext ctx) => _ctx = ctx;

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private async Task<Guid> RegisterFamilyAsync(HttpClient client)
    {
        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Freemium"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.FamilyId;
    }

    private async Task SetFreeInteractionsAsync(Guid familyId, int value)
    {
        using var scope = _ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var family = await db.Families.SingleAsync(f => f.Id == familyId);
        family.FreeInteractionsLeft = value;
        await db.SaveChangesAsync();
    }

    private async Task<int> GetFreeInteractionsAsync(Guid familyId)
    {
        using var scope = _ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var family = await db.Families.AsNoTracking().SingleAsync(f => f.Id == familyId);
        return family.FreeInteractionsLeft;
    }

    [Fact]
    public async Task Evaluation_consumes_credits_and_returns_402_when_exhausted()
    {
        using var client = _ctx.CreateClient();

        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Freemium Eval"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        var familyId = auth!.FamilyId;

        var createChild = await client.PostAsync("/api/family/children", Json(new { name = "Niño", grade = 3 }));
        Assert.Equal(HttpStatusCode.OK, createChild.StatusCode);
        var child = await createChild.Content.ReadFromJsonAsync<ChildProfileDto>();

        var session = await client.PostAsync($"/api/family/children/{child!.Id}/session", null);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // Dos créditos gratuitos.
        await SetFreeInteractionsAsync(familyId, 2);

        // 1ª y 2ª evaluación consumen los créditos (2 → 0).
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/evaluation/evaluate", Json(new { text = "5 más 3 es 8", attempt = 1 }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/evaluation/evaluate", Json(new { text = "5 más 3 es 8", attempt = 1 }))).StatusCode);
        Assert.Equal(0, await GetFreeInteractionsAsync(familyId));

        // 3ª evaluación: sin créditos → 402 y sin consumo negativo.
        Assert.Equal(HttpStatusCode.PaymentRequired, (await client.PostAsync("/api/evaluation/evaluate", Json(new { text = "5 más 3 es 8", attempt = 1 }))).StatusCode);
        Assert.Equal(0, await GetFreeInteractionsAsync(familyId));
    }

    [Fact]
    public async Task Chat_returns_402_when_free_interactions_exhausted()
    {
        using var client = _ctx.CreateClient();
        var familyId = await RegisterFamilyAsync(client);

        await SetFreeInteractionsAsync(familyId, 0);

        var response = await client.PostAsync("/api/agent/chat", Json(new { message = "Hola LUMI" }));

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task Premium_family_never_runs_out_of_interactions()
    {
        using var client = _ctx.CreateClient();
        var familyId = await RegisterFamilyAsync(client);

        // Premium y con créditos agotados: el límite no debería aplicarse.
        using (var scope = _ctx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var family = await db.Families.SingleAsync(f => f.Id == familyId);
            family.IsPremium = true;
            family.FreeInteractionsLeft = 0;
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsync("/api/agent/chat", Json(new { message = "Hola LUMI" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
