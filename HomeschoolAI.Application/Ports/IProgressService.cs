using HomeschoolAI.Application.Progress;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de entrada: persistencia del progreso de aprendizaje (XP y nivel).
/// La implementación vive en Adapters porque usa <c>AppDbContext</c>.
/// </summary>
public interface IProgressService
{
    /// <summary>
    /// Busca el <see cref="Domain.Progress.ChildProgress"/> del niño (o lo crea si es
    /// su primera vez), suma los XP y recalcula el nivel de forma determinista.
    /// </summary>
    Task<ProgressResult> AddXpAsync(Guid childProfileId, int xpToAdd, CancellationToken cancellationToken = default);
}
