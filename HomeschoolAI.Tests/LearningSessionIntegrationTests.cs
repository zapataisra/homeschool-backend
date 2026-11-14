using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Auth;
using HomeschoolAI.Application.Learning;
using Xunit;

namespace HomeschoolAI.Tests;

[Collection(IntegrationCollection.Name)]
public class LearningSessionIntegrationTests
{
    private readonly IdentityTestContext _ctx;

    public LearningSessionIntegrationTests(IdentityTestContext ctx) => _ctx = ctx;

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private async Task<(Guid FamilyId, Guid ChildId)> SetupChildSessionAsync(HttpClient client)
    {
        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Aprendizaje"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();

        var createChild = await client.PostAsync("/api/family/children", Json(new { name = "Mía", grade = 3 }));
        Assert.Equal(HttpStatusCode.OK, createChild.StatusCode);
        var child = await createChild.Content.ReadFromJsonAsync<ChildProfileDto>();

        var session = await client.PostAsync($"/api/family/children/{child!.Id}/session", null);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        return (auth!.FamilyId, child.Id);
    }

    [Fact]
    public async Task Session_start_returns_200_with_explorer_lesson_metrics_and_greeting()
    {
        using var client = _ctx.CreateClient();
        await SetupChildSessionAsync(client);

        var response = await client.PostAsync("/api/agent/session/start", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LearningSessionResponse>();

        Assert.NotNull(result);
        Assert.Equal("Mía", result!.Explorer.ChildName);
        Assert.Equal(3, result.Explorer.Grade);
        Assert.NotNull(result.ActiveLesson);
        Assert.Equal("1.0", result.ActiveLesson.SchemaVersion);
        Assert.NotEmpty(result.ActiveLesson.Blocks);
        Assert.False(string.IsNullOrWhiteSpace(result.ActiveLesson.Title));
        Assert.NotNull(result.Metrics);
        Assert.False(string.IsNullOrWhiteSpace(result.ThreadId));
        Assert.False(string.IsNullOrWhiteSpace(result.InitialAxolotlMessage));
    }

    [Fact]
    public async Task Session_start_twice_reuses_thread_without_double_quota()
    {
        using var client = _ctx.CreateClient();
        var (familyId, _) = await SetupChildSessionAsync(client);

        await SetFreeInteractionsAsync(familyId, 1);

        var first = await client.PostAsync("/api/agent/session/start", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsync("/api/agent/session/start", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Solo se consumió un crédito (el segundo refresco reutiliza el saludo).
        Assert.Equal(0, await GetFreeInteractionsAsync(familyId));
    }

    [Fact]
    public async Task Chat_updates_topic_time_spent()
    {
        using var client = _ctx.CreateClient();
        var (_, childId) = await SetupChildSessionAsync(client);

        var session = await client.PostAsync("/api/agent/session/start", null);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // Simula 60 segundos transcurridos desde la última actividad del tema.
        await SetLastActivityAsync(childId, DateTime.UtcNow.AddSeconds(-60));

        var chat = await client.PostAsync("/api/agent/chat", Json(new { message = "¿Me explicas el tema?" }));
        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);

        Assert.True(await GetTopicTimeAsync(childId) >= 50, "El tiempo del tema en curso debería haberse acumulado.");
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

    private async Task SetLastActivityAsync(Guid childId, DateTime lastActivityAtUtc)
    {
        using var scope = _ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var topic = await db.ChildTopicProgresses.SingleAsync(t => t.ChildProfileId == childId);
        topic.LastActivityAtUtc = lastActivityAtUtc;
        await db.SaveChangesAsync();
    }

    private async Task<int> GetTopicTimeAsync(Guid childId)
    {
        using var scope = _ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var topic = await db.ChildTopicProgresses.AsNoTracking().SingleAsync(t => t.ChildProfileId == childId);
        return topic.TimeSpentSeconds;
    }
}