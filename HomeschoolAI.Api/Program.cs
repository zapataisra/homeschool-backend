using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;
using HomeschoolAI.Adapters.Embedding;
using HomeschoolAI.Adapters.Persistence;
using HomeschoolAI.Api.Endpoints;
using HomeschoolAI.Api.Middleware;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Domain.Embedding;
using HomeschoolAI.Domain.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Logging: consola legible en desarrollo, JSON por salida estándar fuera de él.
// Los niveles mínimos por espacio de nombres vienen de appsettings.json.
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(options => options.IncludeScopes = true);
}
else
{
    builder.Logging.AddJsonConsole();
}

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

app.Run();

