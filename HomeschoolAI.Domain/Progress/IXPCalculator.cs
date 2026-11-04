namespace HomeschoolAI.Domain.Progress;

/// <summary>
/// Puerto: cálculo determinista de Puntos de Experiencia (XP).
/// La recompensa se reduce por cada intento extra (máximo 3 intentos).
/// </summary>
public interface IXPCalculator
{
    /// <summary>
    /// Calcula los XP a otorgar por un nodo/misión.
    /// 1.er intento = 100% de la base; 2.º = 75%; 3.º (y superiores) = 50%.
    /// </summary>
    int CalculateXp(int baseXp, int attemptsUsed);
}
