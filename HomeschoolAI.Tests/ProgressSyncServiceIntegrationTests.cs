using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Auth;
using HomeschoolAI.Application.Learning;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Progress;
using Xunit;

namespace HomeschoolAI.Tests;

[Collection(IntegrationCollection.Name)]
public class ProgressSyncServiceIntegrationTests
{
    private readonly IdentityTestContext _ctx;

    public ProgressSyncServiceIntegrationTests(IdentityTestContext ctx) => _ctx = ctx;

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    [Fact]
    public async Task SyncAsync_upserts_progress_idempotently()
    {
        using var client = _ctx.CreateClient();

        var register = await client.PostAsync("/api/auth/register", Json(new
        {
            email = $"owner_{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            familyName = "Familia Sync"
        }));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var createChild = await client.PostAsync("/api/family/children", Json(new { name = "Mía", grade = 3 }));
        Assert.Equal(HttpStatusCode.OK, createChild.StatusCode);
        var child = await createChild.Content.ReadFromJsonAsync<ChildProfileDto>();

        var update = new PendingProgressUpdate(
            child!.Id,
            "fase4-saberes-repartos",
            "Repartos y fracciones equivalentes",
            "Saberes y Pensamiento Científico",
            TopicStatus.Completed,
            Attempts: 1,
            XpAwarded: 100,
            BadgeEarned: "Rayo de Inspiración",
            TotalXp: 100,
            AttemptedAtUtc: DateTime.UtcNow);

        // Aplicar DOS veces la misma actualización: el UPSERT debe ser idempotente.
        using (var scope = _ctx.Factory.Services.CreateScope())
        {
            var sync = scope.ServiceProvider.GetRequiredService<IProgressSyncService>();
            Assert.True(await sync.SyncAsync(update));
            Assert.True(await sync.SyncAsync(update));
        }

        using (var scope = _ctx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var topic = await db.ChildTopicProgresses.AsNoTracking()
                .SingleAsync(t => t.ChildProfileId == child.Id && t.TopicId == "fase4-saberes-repartos");
            Assert.Equal(TopicStatus.Completed, topic.Status);
            Assert.Equal(1, topic.Attempts);
            Assert.Equal("Rayo de Inspiración", topic.BadgeEarned);
            Assert.NotNull(topic.CompletedAtUtc);

            // Un único registro por (niño, tema): el reintento no duplicó la fila.
            var topicCount = await db.ChildTopicProgresses
                .CountAsync(t => t.ChildProfileId == child.Id && t.TopicId == "fase4-saberes-repartos");
            Assert.Equal(1, topicCount);

            // La XP es monotónica: aplicar la misma actualización no la duplicó a 200.
            var progress = await db.ChildProgress.AsNoTracking()
                .SingleAsync(p => p.ChildProfileId == child.Id);
            Assert.Equal(100, progress.TotalXP);
            Assert.Equal(2, progress.CurrentLevel);
        }
    }
}