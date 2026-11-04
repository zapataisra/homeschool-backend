using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Auth;
using HomeschoolAI.Application.Evaluation;
using HomeschoolAI.Domain.Progress;
using Xunit;

namespace HomeschoolAI.Tests;

[Collection(IntegrationCollection.Name)]
public class EvaluationEndpointTests
{
    private readonly IdentityTestContext _ctx;

    public EvaluationEndpointTests(IdentityTestContext ctx) => _ctx = ctx;

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Anonymous_request_returns_401()
    {
        using var client = _ctx.CreateClient();

        var response = await client.PostAsync("/api/evaluation/evaluate", Json(new { text = "5 más 3 es 8", attempt = 1 }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Owner_returns_403()
    {
        using var client = _ctx.CreateClient();

        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Owner"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var response = await client.PostAsync("/api/evaluation/evaluate", Json(new { text = "5 más 3 es 8", attempt = 1 }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Child_session_returns_200_with_evaluation_dto()
    {
        using var client = _ctx.CreateClient();

        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Evaluacion"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var createChild = await client.PostAsync("/api/family/children", Json(new { name = "Niño", grade = 3 }));
        Assert.Equal(HttpStatusCode.OK, createChild.StatusCode);
        var child = await createChild.Content.ReadFromJsonAsync<ChildProfileDto>();

        var session = await client.PostAsync($"/api/family/children/{child!.Id}/session", null);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // Ahora la cookie es Child. El fake del LLM devuelve 5 + 3 = 8 (correcto).
        var response = await client.PostAsync("/api/evaluation/evaluate", Json(new { text = "5 más 3 es 8", attempt = 1 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CriticalEvaluationResult>();

        Assert.NotNull(result);
        Assert.True(result!.MathIsCorrect);
        Assert.Equal(100, result.XpAwarded);
        Assert.NotNull(result.Evaluation);
        Assert.Equal(100, result.Evaluation!.Precision);
        Assert.Equal(0.9, result.Evaluation.Confidence);
    }

    [Fact]
    public async Task Child_accumulates_xp_and_levels_up_after_successful_evaluations()
    {
        using var client = _ctx.CreateClient();

        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Progreso"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var createChild = await client.PostAsync("/api/family/children", Json(new { name = "Niño", grade = 3 }));
        Assert.Equal(HttpStatusCode.OK, createChild.StatusCode);
        var child = await createChild.Content.ReadFromJsonAsync<ChildProfileDto>();

        var session = await client.PostAsync($"/api/family/children/{child!.Id}/session", null);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // 1ª evaluación correcta: +100 XP → TotalXP=100, nivel 2 (100/100 + 1).
        var first = await client.PostAsync("/api/evaluation/evaluate", Json(new { text = "5 más 3 es 8", attempt = 1 }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var afterFirst = await GetProgressAsync(child.Id);
        Assert.Equal(100, afterFirst.TotalXP);
        Assert.Equal(2, afterFirst.CurrentLevel);

        // 2ª evaluación correcta: +100 XP → TotalXP=200, nivel 3 (200/100 + 1).
        var second = await client.PostAsync("/api/evaluation/evaluate", Json(new { text = "5 más 3 es 8", attempt = 1 }));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var afterSecond = await GetProgressAsync(child.Id);
        Assert.Equal(200, afterSecond.TotalXP);
        Assert.Equal(3, afterSecond.CurrentLevel);
    }

    private async Task<ChildProgress> GetProgressAsync(Guid childProfileId)
    {
        using var scope = _ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ChildProgress.SingleAsync(c => c.ChildProfileId == childProfileId);
    }
}
