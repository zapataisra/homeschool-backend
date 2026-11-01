using HomeschoolAI.Domain.Retrieval;

namespace HomeschoolAI.Application.Search;

/// <summary>
/// Consulta de búsqueda sobre los Programas Sintéticos.
/// </summary>
public sealed class SearchQuery
{
    public string Question { get; init; } = string.Empty;

    public SearchMode Mode { get; init; } = SearchMode.Hybrid;

    public int Limit { get; init; } = 10;

    public int Pool { get; init; } = 20;
}
