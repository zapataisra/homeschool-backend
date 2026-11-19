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
    public async Task Session_start_without_topic_returns_mission_selector_without_consuming_quota()
    {
        using var client = _ctx.CreateClient();
        var (familyId, _) = await SetupChildSessionAsync(client);

        await SetFreeInteractionsAsync(familyId, 1);

        var response = await client.PostAsync("/api/agent/session/start", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LearningSessionResponse>();

        Assert.NotNull(result);
        Assert.Null(result!.ActiveLesson);
        Assert.NotNull(result.AvailableMissions);
        Assert.Equal(3, result.AvailableMissions.Count);
        Assert.Contains(result.AvailableMissions, m => m.IsRecommended);
        Assert.Equal("Mía", result.Explorer.ChildName);
        Assert.NotNull(result.Metrics);
        Assert.Empty(result.InitialAxolotlMessage);

        // No creó tema ni llamó al LLM: la cuota sigue intacta.
        Assert.Equal(1, await GetFreeInteractionsAsync(familyId));
    }

    [Fact]
    public async Task Session_start_with_topic_creates_active_lesson_and_greeting()
    {
        using var client = _ctx.CreateClient();
        await SetupChildSessionAsync(client);

        var response = await client.PostAsync("/api/agent/session/start", Json(new { topicId = "fase4-saberes-ciclo-agua" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LearningSessionResponse>();

        Assert.NotNull(result);
        Assert.NotNull(result!.ActiveLesson);
        Assert.Equal("fase4-saberes-ciclo-agua", result.ActiveLesson.TopicId);
        Assert.False(string.IsNullOrWhiteSpace(result.InitialAxolotlMessage));
        Assert.NotNull(result.Metrics);
    }

    [Fact]
    public async Task Session_start_returns_200_with_explorer_lesson_metrics_and_greeting()
    {
        using var client = _ctx.CreateClient();
        await SetupChildSessionAsync(client);

        // Sin tema en curso: aparece el selector de misiones.
        var selector = await client.PostAsync("/api/agent/session/start", null);
        Assert.Equal(HttpStatusCode.OK, selector.StatusCode);
        var selectorResult = await selector.Content.ReadFromJsonAsync<LearningSessionResponse>();
        Assert.NotNull(selectorResult);
        Assert.Null(selectorResult!.ActiveLesson);
        Assert.Equal(3, selectorResult.AvailableMissions!.Count);

        // El alumno elige la misión recomendada.
        var recommended = selectorResult.AvailableMissions.First(m => m.IsRecommended);
        var response = await client.PostAsync("/api/agent/session/start", Json(new { topicId = recommended.TopicId }));
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

        // Primera llamada: elige misión y genera el saludo (consume 1 crédito).
        var first = await client.PostAsync("/api/agent/session/start", Json(new { topicId = "fase4-saberes-ciclo-agua" }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Segunda llamada: ya hay tema en curso; reutiliza el saludo sin gastar cuota.
        var second = await client.PostAsync("/api/agent/session/start", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        Assert.Equal(0, await GetFreeInteractionsAsync(familyId));
    }

    [Fact]
    public async Task Chat_updates_topic_time_spent()
    {
        using var client = _ctx.CreateClient();
        var (_, childId) = await SetupChildSessionAsync(client);

        var session = await client.PostAsync("/api/agent/session/start", Json(new { topicId = "fase4-saberes-ciclo-agua" }));
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // Simula 60 segundos transcurridos desde la última actividad del tema.
        await SetLastActivityAsync(childId, DateTime.UtcNow.AddSeconds(-60));

        var chat = await client.PostAsync("/api/agent/chat", Json(new { message = "¿Me explicas el tema?" }));
        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);

        Assert.True(await GetTopicTimeAsync(childId) >= 50, "El tiempo del tema en curso debería haberse acumulado.");
    }

    [Fact]
    public async Task Session_start_recovers_persisted_messages_without_calling_llm_again()
    {
        using var client = _ctx.CreateClient();
        var (familyId, _) = await SetupChildSessionAsync(client);

        await SetFreeInteractionsAsync(familyId, 1);

        // Elige misión: genera el saludo inicial (consume 1 crédito).
        var first = await client.PostAsync("/api/agent/session/start", Json(new { topicId = "fase4-saberes-ciclo-agua" }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<LearningSessionResponse>();
        Assert.NotNull(firstResult);
        Assert.NotNull(firstResult!.Messages);
        Assert.Single(firstResult.Messages);
        Assert.False(string.IsNullOrWhiteSpace(firstResult.InitialAxolotlMessage));
        Assert.Equal(0, await GetFreeInteractionsAsync(familyId));

        // Segunda llamada (mismo día): recupera los Messages persistidos sin llamar al LLM.
        var second = await client.PostAsync("/api/agent/session/start", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondResult = await second.Content.ReadFromJsonAsync<LearningSessionResponse>();
        Assert.NotNull(secondResult);
        Assert.NotNull(secondResult!.Messages);
        Assert.Equal(firstResult.Messages.Count, secondResult.Messages.Count);
        Assert.Equal(firstResult.InitialAxolotlMessage, secondResult.InitialAxolotlMessage);
        Assert.Equal(0, await GetFreeInteractionsAsync(familyId));
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
        return await db.ChildTopicProgresses.AsNoTracking()
            .Where(t => t.ChildProfileId == childId)
            .SumAsync(t => t.TimeSpentSeconds);
    }
}