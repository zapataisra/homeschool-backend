using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Domain.Retrieval;

namespace HomeschoolAI.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/search", async (
                SearchRequest request,
                ISearchService service,
                CancellationToken cancellationToken) =>
            {
                var modeText = string.IsNullOrWhiteSpace(request.Mode) ? "hybrid" : request.Mode;

                if (!Enum.TryParse<SearchMode>(modeText, ignoreCase: true, out var mode))
                {
                    return Results.BadRequest(new
                    {
                        error = $"Modo inválido: '{request.Mode}'. Usa 'keyword', 'vector' o 'hybrid'."
                    });
                }

                var query = new SearchQuery
                {
                    Question = request.Question ?? string.Empty,
                    Mode = mode,
                    Limit = request.Limit is > 0 ? request.Limit.Value : 10,
                    Pool = request.Pool is > 0 ? request.Pool.Value : 20
                };

                var results = await service.SearchAsync(query, cancellationToken);
                return Results.Ok(results);
            })
            .WithName("SearchChunks");
    }
}

public sealed class SearchRequest
{
    public string? Question { get; init; }

    public string Mode { get; init; } = "hybrid";

    public int? Limit { get; init; }

    public int? Pool { get; init; }
}
