namespace HomeschoolAI.Tests;

/// <summary>
/// Configuración compartida de las pruebas de integración.
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// Devuelve la cadena de conexión a PostgreSQL. Prioriza la variable de entorno
    /// <c>ConnectionStrings__DefaultConnection</c> (usada al ejecutar las pruebas en Docker)
    /// y, si no existe, construye una local con el rol de solo lectura <c>app_readonly</c>
    /// a partir de variables de entorno, sin contraseñas en el código.
    /// </summary>
    public static string GetConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "vectordb";
        var user = Environment.GetEnvironmentVariable("POSTGRES_READONLY_USER") ?? "app_readonly";
        var password = Environment.GetEnvironmentVariable("POSTGRES_READONLY_PASSWORD") ?? string.Empty;

        return $"Host={host};Port={port};Database={database};Username={user};Password={password}";
    }
}
