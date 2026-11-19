namespace HomeschoolAI.Adapters.Learning;

/// <summary>
/// Puerto del catálogo curricular. Devuelve los temas oficiales de la SEP; la
/// implementación por defecto lee <c>public.curriculum_topics</c> y, si la base de
/// datos no está disponible, respalda con el catálogo estático (<see cref="SeedCurriculumCatalog"/>).
/// </summary>
public interface ICurriculumCatalog
{
    /// <summary>Devuelve los temas activos de un grado, ordenados por <c>order_index</c>.</summary>
    Task<IReadOnlyList<SeedTopic>> GetTopicsForGradeAsync(int grade, CancellationToken cancellationToken = default);

    /// <summary>Devuelve el tema por su identificador, o <c>null</c> si no existe.</summary>
    Task<SeedTopic?> FindAsync(string topicId, CancellationToken cancellationToken = default);
}