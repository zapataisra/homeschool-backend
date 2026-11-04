using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HomeschoolAI.Application.Auth;
using HomeschoolAI.Application.Evaluation;
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
}
