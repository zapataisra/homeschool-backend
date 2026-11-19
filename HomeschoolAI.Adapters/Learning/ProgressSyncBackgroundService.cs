using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Application.Ports;

namespace HomeschoolAI.Adapters.Learning;

/// <summary>
/// Servicio en segundo plano que drena la cola de progreso pendiente cada 10 segundos
/// e intenta persistir las actualizaciones cuando PostgreSQL vuelve a estar disponible.
/// </summary>
public sealed class ProgressSyncBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IPendingProgressSyncQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProgressSyncBackgroundService> _logger;

    public ProgressSyncBackgroundService(
        IPendingProgressSyncQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ProgressSyncBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DrainAsync(stoppingToken);
        }
    }

    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        while (_queue.TryDequeue(out var update))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IProgressSyncService>();
                if (!await sync.SyncAsync(update, cancellationToken))
                {
                    // La BD sigue caída: reintentar en la próxima vuelta.
                    _queue.Enqueue(update);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar progreso pendiente; se reintentará.");
                _queue.Enqueue(update);
                return;
            }
        }
    }
}