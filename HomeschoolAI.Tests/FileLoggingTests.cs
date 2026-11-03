using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Domain.Retrieval;
using Xunit;

namespace HomeschoolAI.Tests;

public class FileLoggingTests
{
    [Fact]
    public async Task Search_writes_correlation_to_file_without_sensitive_text()
    {
        const string secretQuestion = "PREGUNTA_SECRETA_XYZ_123";
        const string secretFragment = "FRAGMENTO_SECRETO_ABC_456";
        const string correlationId = "corr-test-0001";

        var logPath = Path.Combine(Path.GetTempPath(), $"homeschool-{Guid.NewGuid():N}.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(new CompactJsonFormatter(), logPath, flushToDiskInterval: TimeSpan.FromMilliseconds(50))
            .CreateLogger();

        try
        {
            using ILoggerFactory loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: true);
            var searchLogger = loggerFactory.CreateLogger<SearchService>();

            var service = new SearchService(
                new FakeEmbeddingRepository(new SearchOutcome(
                    new[] { new RetrievalResult(1, "fuente.pdf", 1, secretFragment, 0.5) },
                    Degraded: false)),
                new FakeQueryEmbedder(new float[384]),
                searchLogger);

            using (searchLogger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId }))
            {
                await service.SearchAsync(new SearchQuery { Question = secretQuestion, Mode = SearchMode.Hybrid });
            }

            Log.CloseAndFlush();

            var content = File.ReadAllText(logPath);

            Assert.Contains(correlationId, content);
            Assert.Contains("Búsqueda completada", content);
            Assert.DoesNotContain(secretQuestion, content);
            Assert.DoesNotContain(secretFragment, content);
        }
        finally
        {
            Log.CloseAndFlush();
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }
}
