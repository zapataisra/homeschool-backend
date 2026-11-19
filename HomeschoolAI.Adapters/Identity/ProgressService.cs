using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Progress;
using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Persiste el progreso de aprendizaje (XP y nivel) en <see cref="AppDbContext"/>.
/// La regla de nivel es determinista y vive en <see cref="ChildProgress"/> (1 nivel
/// por cada <see cref="ChildProgress.XpPerLevel"/> XP). La decide el código, nunca el LLM.
/// </summary>
public sealed class ProgressService : IProgressService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProgressService> _logger;

    public ProgressService(AppDbContext db, ILogger<ProgressService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ProgressResult> AddXpAsync(
        Guid childProfileId,
        int xpToAdd,
        CancellationToken cancellationToken = default)
    {
        if (xpToAdd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(xpToAdd), "La XP a sumar no puede ser negativa.");
        }

        var progress = await _db.ChildProgress
            .SingleOrDefaultAsync(c => c.ChildProfileId == childProfileId, cancellationToken);

        // Para un registro nuevo, el nivel previo es 1 (el nivel de partida).
        var previousLevel = progress?.CurrentLevel ?? 1;

        if (progress is null)
        {
            progress = new ChildProgress
            {
                ChildProfileId = childProfileId,
                TotalXP = 0,
                CurrentLevel = 1
            };
            _db.ChildProgress.Add(progress);
        }

        progress.TotalXP += xpToAdd;
        progress.CurrentLevel = ChildProgress.CalculateLevel(progress.TotalXP);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "XP otorgada: ChildProfileId={ChildProfileId}, +{XpToAdd}, TotalXP={TotalXp}, Nivel={Level}",
            childProfileId, xpToAdd, progress.TotalXP, progress.CurrentLevel);

        return new ProgressResult(
            progress.ChildProfileId,
            progress.TotalXP,
            progress.CurrentLevel,
            LeveledUp: progress.CurrentLevel > previousLevel);
    }
}
