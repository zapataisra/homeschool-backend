namespace HomeschoolAI.Adapters.Learning;

/// <summary>
/// Catálogo curricular en memoria: delega en el catálogo estático
/// <see cref="SeedCurriculumCatalog"/> (fallback seguro sin base de datos).
/// </summary>
public sealed class StaticCurriculumCatalog : ICurriculumCatalog
{
    public Task<IReadOnlyList<SeedTopic>> GetTopicsForGradeAsync(int grade, CancellationToken cancellationToken = default)
        => Task.FromResult(SeedCurriculumCatalog.GetTopicsForGrade(grade));

    public Task<SeedTopic?> FindAsync(string topicId, CancellationToken cancellationToken = default)
        => Task.FromResult(SeedCurriculumCatalog.TryFind(topicId));
}