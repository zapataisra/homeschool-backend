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

    /// <summary>
    /// Evalúa una expresión aritmética completa (p. ej. <c>"4 * 3 = 12"</c> o
    /// <c>"2/4 &gt; 3/8"</c>) usando aritmética exacta (fracciones con numerador y
    /// denominador enteros). Devuelve <c>false</c> si la cadena está vacía o no es
    /// una expresión matemática válida, sin lanzar excepciones.
    /// </summary>
    bool ValidateExpression(string expression);

    /// <summary>
    /// Extrae fracciones de una respuesta en lenguaje natural —con barra (<c>a/b</c>) o
    /// descritas en español (<c>"dividirla en D y comer N"</c> → N/D, <c>"repartir X entre Y"</c>
    /// → X/Y, <c>"N de D partes"</c> → N/D)— y comprueba con producto cruzado si las fracciones
    /// propuestas como equivalentes lo son realmente. Devuelve <c>null</c> si no hay suficientes
    /// fracciones o no se afirma equivalencia (el Paso A no aplica), <c>true</c> si son
    /// equivalentes y <c>false</c> si NO lo son.
    /// </summary>
    bool? ValidateFractionEquivalence(string response);
}
