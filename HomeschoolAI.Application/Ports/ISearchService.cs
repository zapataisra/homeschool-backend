using HomeschoolAI.Application.Dtos;
using HomeschoolAI.Application.Search;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de entrada: búsqueda semántica/léxica/híbrida sobre los Programas Sintéticos.
/// </summary>
public interface ISearchService
{
    Task<IReadOnlyList<SearchChunkDto>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default);
}
