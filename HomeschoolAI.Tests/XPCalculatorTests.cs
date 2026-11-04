using HomeschoolAI.Application.Progress;
using Xunit;

namespace HomeschoolAI.Tests;

public class XPCalculatorTests
{
    private readonly XPCalculator _calculator = new();

    [Theory]
    [InlineData(100, 1, 100)]
    [InlineData(100, 2, 75)]
    [InlineData(100, 3, 50)]
    [InlineData(10, 2, 7)]   // 10 * 3 / 4 = 7 (truncado)
    [InlineData(10, 3, 5)]   // 10 / 2 = 5
    [InlineData(4, 2, 3)]    // 4 * 3 / 4 = 3
    public void Calculates_xp_with_deductions(int baseXp, int attempts, int expected)
    {
        Assert.Equal(expected, _calculator.CalculateXp(baseXp, attempts));
    }

    [Fact]
    public void First_attempt_awards_full_base_xp()
    {
        Assert.Equal(123, _calculator.CalculateXp(123, 1));
    }

    [Fact]
    public void More_than_three_attempts_clamps_to_50_percent()
    {
        Assert.Equal(50, _calculator.CalculateXp(100, 4));
        Assert.Equal(50, _calculator.CalculateXp(100, 99));
    }

    [Fact]
    public void Zero_or_negative_attempts_award_nothing()
    {
        Assert.Equal(0, _calculator.CalculateXp(100, 0));
        Assert.Equal(0, _calculator.CalculateXp(100, -1));
    }

    [Fact]
    public void Zero_or_negative_base_xp_awards_nothing()
    {
        Assert.Equal(0, _calculator.CalculateXp(0, 1));
        Assert.Equal(0, _calculator.CalculateXp(-10, 1));
    }
}
