using HomeschoolAI.Application.Learning;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Persistencia idempotente de una actualización de progreso pendiente.
/// Devuelve <c>true</c> si se persistió correctamente.
/// </summary>
public interface IProgressSyncService
{
    Task<bool> SyncAsync(PendingProgressUpdate update, CancellationToken cancellationToken = default);
}