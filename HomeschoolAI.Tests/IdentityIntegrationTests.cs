using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Auth;
using Xunit;

namespace HomeschoolAI.Tests;

public class IdentityIntegrationTests : IClassFixture<IdentityTestContext>
{
    private readonly IdentityTestContext _ctx;

    public IdentityIntegrationTests(IdentityTestContext ctx) => _ctx = ctx;

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Protected_endpoints_require_authentication()
    {
        using var client = _ctx.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/family")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/family/children")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/family/billing")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/family/me")).StatusCode);
    }

    [Fact]
    public async Task Owner_can_access_family_and_child_session_is_limited()
    {
        using var client = _ctx.CreateClient();

        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Owner"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/family")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/family/children")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/family/billing")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/family/me")).StatusCode);

        var createChild = await client.PostAsync("/api/family/children", Json(new { name = "Niño", grade = 3 }));
        Assert.Equal(HttpStatusCode.OK, createChild.StatusCode);
        var child = await createChild.Content.ReadFromJsonAsync<ChildProfileDto>();

        var session = await client.PostAsync($"/api/family/children/{child!.Id}/session", null);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // La cookie ahora es la sesión delegada del niño (rol Child).
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/family/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/family")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/family/billing")).StatusCode);
    }

    [Fact]
    public async Task Guardian_cannot_access_owner_only_endpoints()
    {
        using var client = _ctx.CreateClient();

        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia con guardian"
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

        Assert.Equal(HttpStatusCode.OK, (await guardianClient.GetAsync("/api/family")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guardianClient.GetAsync("/api/family/billing")).StatusCode);
    }

    [Fact]
    public async Task Idor_family_a_cannot_access_family_b_child()
    {
        using var clientA = _ctx.CreateClient();
        var registerA = await clientA.PostAsync("/api/auth/register", Json(new
        {
            email = $"a_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia A"
        }));
        Assert.Equal(HttpStatusCode.OK, registerA.StatusCode);

        var createChild = await clientA.PostAsync("/api/family/children", Json(new { name = "Niño A", grade = 3 }));
        Assert.Equal(HttpStatusCode.OK, createChild.StatusCode);
        var childA = await createChild.Content.ReadFromJsonAsync<ChildProfileDto>();

        using var clientB = _ctx.CreateClient();
        var registerB = await clientB.PostAsync("/api/auth/register", Json(new
        {
            email = $"b_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia B"
        }));
        Assert.Equal(HttpStatusCode.OK, registerB.StatusCode);

        // B intenta iniciar la sesión delegada del hijo de A → 404 (aislamiento por FamilyId).
        var attempt = await clientB.PostAsync($"/api/family/children/{childA!.Id}/session", null);
        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
    }
}
