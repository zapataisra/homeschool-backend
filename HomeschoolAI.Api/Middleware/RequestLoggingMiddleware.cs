using System.Diagnostics;

namespace HomeschoolAI.Api.Middleware;

public sealed partial class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        await _next(context);
        stopwatch.Stop();

        // Plantilla de ruta (no la URL con la consulta) y sin cuerpo.
        var routeTemplate = context.GetEndpoint() is RouteEndpoint route
            ? route.RoutePattern.RawText!
            : context.Request.Path.ToString();

        LogRequest(_logger, context.Request.Method, routeTemplate, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "HTTP {Method} {Route} respondió {StatusCode} en {ElapsedMs}ms")]
    private static partial void LogRequest(ILogger logger, string method, string route, int statusCode, long elapsedMs);
}
