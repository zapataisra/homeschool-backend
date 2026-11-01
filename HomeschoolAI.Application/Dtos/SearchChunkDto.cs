namespace HomeschoolAI.Application.Dtos;

/// <summary>
/// DTO devuelto por la búsqueda de fragmentos curriculares.
/// </summary>
public sealed record SearchChunkDto(
    int Id,
    string Source,
    int ChunkId,
    string Texto,
    double Score);
