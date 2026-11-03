using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using HomeschoolAI.Adapters.Embedding;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Adapters.Persistence;
using HomeschoolAI.Adapters.SemanticKernel;
using HomeschoolAI.Api.Endpoints;
using HomeschoolAI.Api.Middleware;
using HomeschoolAI.Application.Chat;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Application.Telemetry;
using HomeschoolAI.Domain.Conversation;
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

// Conexión de escritura/DDL para el contexto de identidad (esquema app_identity).
// DefaultConnection (solo lectura) se mantiene intacta para el RAG sobre embeddings_sep.
var identityConnectionString = builder.Configuration.GetConnectionString("IdentityConnection")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena de conexión de identidad. Configura 'ConnectionStrings:IdentityConnection' "
        + "(o la variable de entorno ConnectionStrings__IdentityConnection).");

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

// Identidad y accesos: contexto de escritura en el esquema `app_identity`.
// Usa IdentityConnection (escritura/DDL); DefaultConnection (solo lectura) queda para el RAG.
builder.Services.AddHomeschoolIdentity(identityConnectionString);

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
// Singleton: SearchService y EmbeddingRepository son sin estado y thread-safe
// (solo dependen de NpgsqlDataSource e IQueryEmbedder, ambos Singleton), lo que
// permite que el agente (también Singleton) los componga sin desajuste de lifetimes.
builder.Services.AddSingleton<IEmbeddingRepository, EmbeddingRepository>();
builder.Services.AddSingleton<ISearchService, SearchService>();

// Semantic Kernel: Kernel Singleton con el chat completion de OpenAI y el plugin de currículo.
// El plugin llama al puerto de entrada ISearchService (nunca al DbContext).
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var openAiModelId = builder.Configuration["OpenAI:ModelId"]
    ?? Environment.GetEnvironmentVariable("OPENAI_MODEL_ID")
    ?? "gpt-4o-mini";

builder.Services.AddSingleton<CurriculumPlugin>();
builder.Services.AddSingleton<IAgentCompletionService, SemanticKernelAgentCompletionService>();
builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
builder.Services.AddSingleton<IChatService, ChatService>();

builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    if (!string.IsNullOrWhiteSpace(openAiApiKey))
    {
        kernelBuilder.AddOpenAIChatCompletion(modelId: openAiModelId, apiKey: openAiApiKey);
    }
    else
    {
        sp.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SemanticKernel")
            .LogWarning("OPENAI_API_KEY no está configurada: el endpoint /api/agent/chat responderá con error hasta que se defina.");
    }

    var kernel = kernelBuilder.Build();
    kernel.Plugins.AddFromObject(sp.GetRequiredService<CurriculumPlugin>(), "curriculum");
    return kernel;
});

// Limitación de tasa para el endpoint que llama a un LLM (OWASP: disponibilidad y coste).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("agent", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

// Middlewares: correlación → registro de petición → manejo de errores.
app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Limitación de tasa (antes de los endpoints).
app.UseRateLimiter();

// Autenticación y autorización (cookies).
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "HomeschoolAI API");
app.MapSearchEndpoints();
app.MapAgentEndpoints();
app.MapAuthEndpoints();
app.MapFamilyEndpoints();

app.Logger.LogInformation("HomeschoolAI API arrancada en el entorno {Environment}", app.Environment.EnvironmentName);

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

// Expone la clase Program para las pruebas de integración (WebApplicationFactory).
public partial class Program
{
}

