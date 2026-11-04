using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Application.Evaluation;
using HomeschoolAI.Application.Progress;
using HomeschoolAI.Application.Validation;
using HomeschoolAI.Domain.Progress;
using HomeschoolAI.Domain.Validation;

namespace HomeschoolAI.Application.DependencyInjection;

/// <summary>
/// Registro de dependencias del proyecto Application (servicios deterministas y orquestadores).
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Servicios deterministas (sin LLM): validación aritmética y cálculo de XP.
        services.AddSingleton<IMathValidator, MathValidator>();
        services.AddSingleton<IXPCalculator, XPCalculator>();

        // Orquestador del Agente Crítico.
        services.AddSingleton<CriticalEvaluationService>();

        return services;
    }
}
