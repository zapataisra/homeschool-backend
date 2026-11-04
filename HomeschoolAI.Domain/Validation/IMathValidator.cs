namespace HomeschoolAI.Domain.Validation;

/// <summary>
/// Puerto: validación determinista (sin LLM) de operaciones aritméticas básicas.
/// Evita alucinaciones numéricas del agente crítico.
/// </summary>
public interface IMathValidator
{
    /// <summary>
    /// Comprueba si <paramref name="proposedResult"/> es el resultado correcto de
    /// <c>operand1 [operation] operand2</c>. Las operaciones soportadas son
    /// <c>+</c>, <c>-</c>, <c>*</c> (también ×, x, ·) y <c>/</c> (también ÷).
    /// La división por cero devuelve <c>false</c>.
    /// </summary>
    bool ValidateOperation(decimal operand1, string operation, decimal operand2, decimal proposedResult);
}
