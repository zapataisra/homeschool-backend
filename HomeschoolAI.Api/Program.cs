using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using HomeschoolAI.Adapters.Embedding;
using HomeschoolAI.Adapters.Persistence;
using HomeschoolAI.Api.Endpoints;
using HomeschoolAI.Api.Middleware;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Application.Telemetry;
using HomeschoolAI.Domain.Embedding;
using HomeschoolAI.Domain.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Serilog como proveedor de ILogger: consola + archivo JSON compacto (rotación diaria, 14 días).
var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext();

if (builder.Environment.IsDevelopment())
{
    loggerConfiguration
        .MinimumLevel.Override("HomeschoolAI", LogEventLevel.Debug)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} => CorrelationId: {CorrelationId}{NewLine}{Exception}");
}
else
{
    loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
}

loggerConfiguration.WriteTo.File(
    new CompactJsonFormatter(),
    "/app/logs/app-.log",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 14,
    flushToDiskInterval: TimeSpan.FromSeconds(2));

Log.Logger = loggerConfiguration.CreateLogger();
builder.Host.UseSerilog();

// OpenTelemetry: trazado distribuido (HTTP + spans de negocio) con exportador a consola.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("HomeschoolAI"))
    .WithTracing(tracing => tracing
        .AddSource(ApplicationTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena de conexión. Configura 'ConnectionStrings:DefaultConnection' "
        + "(o la variable de entorno ConnectionStrings__DefaultConnection).");

// pgvector (Npgsql): registra el tipo `vector` para las consultas crudas del repositorio.
builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    return dataSourceBuilder.Build();
});

// EF Core: mapeo de solo lectura sobre la tabla existente `embeddings_sep`.
// NO se ejecutan migraciones: la BD fue creada y poblada desde el pipeline Python.
builder.Services.AddDbContext<HomeschoolDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

// Embedder ONNX: Singleton. InferenceSession es costoso de crear y thread-safe;
// el contenedor lo desecha (Dispose) una sola vez al apagar la aplicación.
var modelDir = builder.Configuration["Embedding:ModelDir"]
    ?? Environment.GetEnvironmentVariable("EMBEDDING_MODEL_DIR")
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "models", "all-MiniLM-L6-v2"));

builder.Services.AddSingleton<IQueryEmbedder>(sp => new OnnxQueryEmbedder(
    Path.Combine(modelDir, "model.onnx"),
    Path.Combine(modelDir, "vocab.txt"),
    sp.GetRequiredService<ILogger<OnnxQueryEmbedder>>()));

// Puertos de la arquitectura hexagonal.
builder.Services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
builder.Services.AddScoped<ISearchService, SearchService>();

var app = builder.Build();

// Middlewares: correlación → registro de petición → manejo de errores.
app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/", () => "HomeschoolAI API");
app.MapSearchEndpoints();

app.Logger.LogInformation("HomeschoolAI API arrancada en el entorno {Environment}", app.Environment.EnvironmentName);

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

