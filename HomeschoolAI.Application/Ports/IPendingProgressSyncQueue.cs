using HomeschoolAI.Application.Learning;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Cola thread-safe de actualizaciones de progreso pendientes de persistir
/// (se llenan cuando una escritura a PostgreSQL falla por conexión/BD).
/// </summary>
public interface IPendingProgressSyncQueue
{
    void Enqueue(PendingProgressUpdate update);

    bool TryDequeue(out PendingProgressUpdate update);

    int Count { get; }
}