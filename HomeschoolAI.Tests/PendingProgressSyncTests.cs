using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Adapters.Learning;
using HomeschoolAI.Application.Learning;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Progress;
using Xunit;

namespace HomeschoolAI.Tests;

public class PendingProgressSyncTests
{
    private static PendingProgressUpdate CreateUpdate(Guid childId, string topicId, int totalXp = 100) => new(
        childId,
        topicId,
        "Título de ejemplo",
        "Saberes y Pensamiento Científico",
        TopicStatus.Completed,
        Attempts: 1,
        XpAwarded: 100,
        BadgeEarned: "Rayo de Inspiración",
        TotalXp: totalXp,
        AttemptedAtUtc: DateTime.UtcNow);

    [Fact]
    public void Queue_enqueues_and_dequeues_in_order()
    {
        var queue = new PendingProgressSyncQueue();
        var first = CreateUpdate(Guid.NewGuid(), "tema-1");
        var second = CreateUpdate(Guid.NewGuid(), "tema-2");

        queue.Enqueue(first);
        queue.Enqueue(second);

        Assert.Equal(2, queue.Count);
        Assert.True(queue.TryDequeue(out var a));
        Assert.True(queue.TryDequeue(out var b));
        Assert.False(queue.TryDequeue(out _));
        Assert.Same(first, a);
        Assert.Same(second, b);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task Drain_keeps_pending_item_while_database_down_and_drains_when_it_recovers()
    {
        var queue = new PendingProgressSyncQueue();
        var update = CreateUpdate(Guid.NewGuid(), "tema-1");
        queue.Enqueue(update);

        // 1) BD caída: el drenaje no puede persistir y el elemento permanece en la cola.
        var down = new FakeProgressSyncService(succeed: false);
        var serviceDown = new ProgressSyncBackgroundService(
            queue,
            new FakeScopeFactory(down),
            NullLogger<ProgressSyncBackgroundService>.Instance);
        await serviceDown.DrainAsync();

        Assert.Equal(1, queue.Count);
        Assert.Empty(down.Synced);

        // 2) BD recuperada: el drenaje persiste y retira el elemento de la cola.
        var up = new FakeProgressSyncService(succeed: true);
        var serviceUp = new ProgressSyncBackgroundService(
            queue,
            new FakeScopeFactory(up),
            NullLogger<ProgressSyncBackgroundService>.Instance);
        await serviceUp.DrainAsync();

        Assert.Equal(0, queue.Count);
        Assert.Single(up.Synced);
        Assert.Equal(update, up.Synced[0]);
    }

    [Fact]
    public async Task SyncAsync_returns_false_when_database_is_unavailable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=vectordb;Username=admin;Password=x;Timeout=2")
            .Options;
        await using var db = new AppDbContext(options);
        var service = new ProgressSyncService(db, NullLogger<ProgressSyncService>.Instance);

        var ok = await service.SyncAsync(CreateUpdate(Guid.NewGuid(), "tema-1"));

        Assert.False(ok);
    }

    [Fact]
    public void Queue_dedupes_updates_for_same_child_and_topic_keeping_most_recent()
    {
        var queue = new PendingProgressSyncQueue();
        var childId = Guid.NewGuid();
        var older = CreateUpdate(childId, "tema-1", totalXp: 50);
        var newer = CreateUpdate(childId, "tema-1", totalXp: 100);

        queue.Enqueue(older);
        queue.Enqueue(newer);

        Assert.Equal(1, queue.Count);
        Assert.True(queue.TryDequeue(out var result));
        Assert.Same(newer, result);
        Assert.Equal(100, result.TotalXp);
        Assert.False(queue.TryDequeue(out _));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Queue_caps_at_max_capacity_evicting_oldest()
    {
        var queue = new PendingProgressSyncQueue();
        var childId = Guid.NewGuid();

        for (var i = 0; i <= PendingProgressSyncQueue.MaxCapacity; i++)
        {
            queue.Enqueue(CreateUpdate(childId, $"tema-{i}", totalXp: i));
        }

        Assert.Equal(PendingProgressSyncQueue.MaxCapacity, queue.Count);

        // El primer tema (tema-0) fue evictado por compactación; el siguiente en salir es tema-1.
        Assert.True(queue.TryDequeue(out var first));
        Assert.Equal("tema-1", first.TopicId);
    }

    private sealed class FakeProgressSyncService : IProgressSyncService
    {
        private readonly bool _succeed;

        public List<PendingProgressUpdate> Synced { get; } = new();

        public FakeProgressSyncService(bool succeed) => _succeed = succeed;

        public Task<bool> SyncAsync(PendingProgressUpdate update, CancellationToken cancellationToken = default)
        {
            if (_succeed)
            {
                Synced.Add(update);
            }

            return Task.FromResult(_succeed);
        }
    }

    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly IProgressSyncService _service;

        public FakeScopeFactory(IProgressSyncService service) => _service = service;

        public IServiceScope CreateScope() => new FakeScope(_service);

        private sealed class FakeScope : IServiceScope
        {
            public FakeScope(IProgressSyncService service) => ServiceProvider = new FakeServiceProvider(service);

            public IServiceProvider ServiceProvider { get; }

            public void Dispose()
            {
            }
        }

        private sealed class FakeServiceProvider : IServiceProvider
        {
            private readonly IProgressSyncService _service;

            public FakeServiceProvider(IProgressSyncService service) => _service = service;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(IProgressSyncService) ? _service : null;
        }
    }
}