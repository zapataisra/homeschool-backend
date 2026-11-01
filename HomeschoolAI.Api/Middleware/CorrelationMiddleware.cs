using System.Diagnostics;

namespace HomeschoolAI.Api.Middleware;

public sealed class CorrelationMiddleware
{
    public const string CorrelationIdKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.Items[CorrelationIdKey] = correlationId;

        using (_logger.BeginScope("CorrelationId: {CorrelationId}", correlationId))
        {
            await _next(context);
        }
    }
}
