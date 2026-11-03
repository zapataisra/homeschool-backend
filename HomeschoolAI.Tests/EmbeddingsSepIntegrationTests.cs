using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using HomeschoolAI.Adapters.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace HomeschoolAI.Tests;

public class EmbeddingsSepIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public EmbeddingsSepIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task EmbeddingsSep_has_699_rows()
    {
        // Localmente (BD en localhost:5432) configura las variables POSTGRES_* (rol app_readonly).
        // En Docker se sobrescribe con ConnectionStrings__DefaultConnection.
        var connectionString = TestConfiguration.GetConnectionString();

        var stopwatch = Stopwatch.StartNew();

        var options = new DbContextOptionsBuilder<HomeschoolDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        await using var dbContext = new HomeschoolDbContext(options);
        var count = await dbContext.EmbeddingsSep.CountAsync();

        stopwatch.Stop();

        _output.WriteLine($"Filas en embeddings_sep: {count}");
        _output.WriteLine($"Tiempo total (cold start EF Core + primera conexion + consulta): {stopwatch.ElapsedMilliseconds} ms");

        Assert.Equal(699, count);
    }
}
