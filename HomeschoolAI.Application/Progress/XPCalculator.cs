using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Application.Progress;

/// <summary>
/// Implementación determinista de <see cref="IXPCalculator"/>.
/// Usa aritmética entera para que la deducción de XP sea exacta.
/// </summary>
public sealed class XPCalculator : IXPCalculator
{
    public int CalculateXp(int baseXp, int attemptsUsed)
    {
        if (baseXp <= 0)
        {
            return 0;
        }

        // Máximo 3 intentos. Recompensa: 1.º = 100%, 2.º = 75%, 3.º (o más) = 50%.
        return attemptsUsed switch
        {
            1 => baseXp,
            2 => baseXp * 3 / 4,
            3 => baseXp / 2,
            > 3 => baseXp / 2,
            _ => 0
        };
    }
}
