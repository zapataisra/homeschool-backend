using Microsoft.AspNetCore.Mvc;
using HomeschoolAI.Domain.Exceptions;

namespace HomeschoolAI.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Error de dominio {StatusCode} procesando la petición: {Message}", ex.StatusCode, ex.Message);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var correlationId = context.Items[CorrelationMiddleware.CorrelationIdKey]?.ToString()
                ?? context.TraceIdentifier;

            var problem = new ProblemDetails
            {
                Status = ex.StatusCode,
                Title = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5",
                Instance = context.Request.Path
            };
            problem.Extensions["correlationId"] = correlationId;

            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado procesando la petición");

            if (context.Response.HasStarted)
            {
                throw;
            }

            var correlationId = context.Items[CorrelationMiddleware.CorrelationIdKey]?.ToString()
                ?? context.TraceIdentifier;

            // ProblemDetails sin traza de pila; el detalle queda solo en los logs.
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Se produjo un error inesperado.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Instance = context.Request.Path
            };
            problem.Extensions["correlationId"] = correlationId;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
