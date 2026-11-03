using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using HomeschoolAI.Adapters.Identity;
using Xunit;

namespace HomeschoolAI.Tests;

/// <summary>
/// Contexto de las pruebas de integración de identidad: crea una base de pruebas
/// desechable (<c>vectordb_test</c>), le aplica las migraciones y expone un
/// <see cref="WebApplicationFactory{TEntryPoint}"/> apuntando a esa base.
/// </summary>
public sealed class IdentityTestContext : IAsyncLifetime
{
    public const string TestDatabaseName = "vectordb_test";

    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory!;

    public async Task InitializeAsync()
    {
        var testConnection = await EnsureDatabaseAsync();

        // Program.cs lee ConnectionStrings:IdentityConnection de la configuración
        // (variables de entorno). No toco DefaultConnection: lo usan otras pruebas.
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityConnection", testConnection);

        _factory = new ApiWebApplicationFactory();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>Crea un cliente HTTP aislado (cookies propias) para simular una sesión.</summary>
    public HttpClient CreateClient() => _factory!.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static async Task<string> EnsureDatabaseAsync()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "host.docker.internal";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "admin";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "fUYK5wtjxE7WvICX0mgDkrFd";

        var adminConnection = $"Host={host};Port={port};Database=vectordb;Username={user};Password={password}";
        var testConnection = $"Host={host};Port={port};Database={TestDatabaseName};Username={user};Password={password}";

        await EnsureDatabaseCreatedAsync(adminConnection);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(testConnection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        return testConnection;
    }

    private static async Task EnsureDatabaseCreatedAsync(string adminConnection)
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();

        await using var check = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @name", connection);
        check.Parameters.AddWithValue("@name", TestDatabaseName);
        var exists = await check.ExecuteScalarAsync() is not null;

        if (!exists)
        {
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{TestDatabaseName}\"", connection);
            await create.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>
/// Fábrica del host de pruebas: apunta <c>AppDbContext</c> a la base de pruebas
/// y mantiene <c>DefaultConnection</c> para que el arranque no falle.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
