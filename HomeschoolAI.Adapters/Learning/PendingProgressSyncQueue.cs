using HomeschoolAI.Application.Learning;
using HomeschoolAI.Application.Ports;

namespace HomeschoolAI.Adapters.Learning;

/// <summary>
/// Cola thread-safe de actualizaciones de progreso pendientes. Deduplica por
/// (ChildProfileId, TopicId) conservando la actualización más reciente (idempotente)
/// y limita el número máximo de elementos para no crecer sin control en memoria.
/// </summary>
public sealed class PendingProgressSyncQueue : IPendingProgressSyncQueue
{
    /// <summary>Límite máximo de actualizaciones pendientes distintas retenidas en memoria.</summary>
    public const int MaxCapacity = 500;

    private readonly object _gate = new();
    private readonly LinkedList<PendingProgressUpdate> _order = new();
    private readonly Dictionary<(Guid ChildId, string TopicId), LinkedListNode<PendingProgressUpdate>> _byKey = new();

    public void Enqueue(PendingProgressUpdate update)
    {
        var key = (update.ChildProfileId, update.TopicId);

        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out var existing))
            {
                // Ya hay una actualización pendiente para este (niño, tema): se conserva
                // la más reciente y se reubica al final para respetar el orden FIFO.
                existing.Value = update;
                _order.Remove(existing);
                _order.AddLast(existing);
                return;
            }

            var node = _order.AddLast(update);
            _byKey[key] = node;

            // Compactación: si se supera el límite, evicta la actualización más antigua.
            while (_order.Count > MaxCapacity)
            {
                var oldest = _order.First!;
                _order.RemoveFirst();
                _byKey.Remove((oldest.Value.ChildProfileId, oldest.Value.TopicId));
            }
        }
    }

    public bool TryDequeue(out PendingProgressUpdate update)
    {
        lock (_gate)
        {
            if (_order.First is null)
            {
                update = null!;
                return false;
            }

            var node = _order.First;
            update = node.Value;
            _order.RemoveFirst();
            _byKey.Remove((update.ChildProfileId, update.TopicId));
            return true;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _order.Count;
            }
        }
    }
}