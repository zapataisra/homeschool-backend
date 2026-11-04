using HomeschoolAI.Domain.Validation;

namespace HomeschoolAI.Application.Validation;

/// <summary>
/// Implementación determinista de <see cref="IMathValidator"/>.
/// Usa aritmética <c>decimal</c> (sin punto flotante) para evitar errores de precisión.
/// </summary>
public sealed class MathValidator : IMathValidator
{
    public bool ValidateOperation(decimal operand1, string operation, decimal operand2, decimal proposedResult)
    {
        var op = Normalize(operation);
        if (op is null)
        {
            return false;
        }

        return op switch
        {
            "+" => operand1 + operand2 == proposedResult,
            "-" => operand1 - operand2 == proposedResult,
            "*" => operand1 * operand2 == proposedResult,
            "/" => operand2 != 0m && operand1 / operand2 == proposedResult,
            _ => false
        };
    }

    private static string? Normalize(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return null;
        }

        return operation.Trim().ToLowerInvariant() switch
        {
            "×" or "x" or "·" => "*",
            "÷" => "/",
            var op => op
        };
    }
}
