using HomeschoolAI.Domain.Validation;
using Xunit;

namespace HomeschoolAI.Tests;

public class MathValidatorTests
{
    private readonly IMathValidator _validator = new DeterministicMathValidator();

    [Theory]
    [InlineData(2, "+", 3, 5, true)]
    [InlineData(2, "+", 3, 6, false)]
    [InlineData(10, "-", 4, 6, true)]
    [InlineData(10, "-", 4, 5, false)]
    [InlineData(4, "*", 5, 20, true)]
    [InlineData(4, "*", 5, 19, false)]
    [InlineData(20, "/", 4, 5, true)]
    [InlineData(20, "/", 4, 4, false)]
    public void Validates_basic_arithmetic(decimal a, string op, decimal b, decimal result, bool expected)
    {
        Assert.Equal(expected, _validator.ValidateOperation(a, op, b, result));
    }

    [Fact]
    public void Division_by_zero_returns_false()
    {
        Assert.False(_validator.ValidateOperation(5m, "/", 0m, 5m));
        Assert.False(_validator.ValidateOperation(5m, "/", 0m, 0m));
    }

    [Fact]
    public void Unknown_operation_returns_false()
    {
        Assert.False(_validator.ValidateOperation(2m, "%", 3m, 6m));
        Assert.False(_validator.ValidateOperation(2m, "^", 3m, 8m));
        Assert.False(_validator.ValidateOperation(2m, "potencia", 3m, 8m));
    }

    [Fact]
    public void Null_or_empty_operation_returns_false()
    {
        Assert.False(_validator.ValidateOperation(2m, null!, 3m, 5m));
        Assert.False(_validator.ValidateOperation(2m, "", 3m, 5m));
        Assert.False(_validator.ValidateOperation(2m, "   ", 3m, 5m));
    }

    [Fact]
    public void Supports_multiplication_symbol_aliases()
    {
        Assert.True(_validator.ValidateOperation(4m, "×", 5m, 20m));
        Assert.True(_validator.ValidateOperation(4m, "x", 5m, 20m));
        Assert.True(_validator.ValidateOperation(4m, "X", 5m, 20m));
        Assert.True(_validator.ValidateOperation(4m, "·", 5m, 20m));
    }

    [Fact]
    public void Supports_division_symbol_alias()
    {
        Assert.True(_validator.ValidateOperation(20m, "÷", 4m, 5m));
    }

    [Fact]
    public void Handles_negative_operands()
    {
        Assert.True(_validator.ValidateOperation(-2m, "+", 3m, 1m));
        Assert.True(_validator.ValidateOperation(-2m, "*", 3m, -6m));
        Assert.True(_validator.ValidateOperation(6m, "/", -2m, -3m));
    }

    [Fact]
    public void Handles_decimal_operands()
    {
        Assert.True(_validator.ValidateOperation(1.5m, "*", 2m, 3m));
        Assert.True(_validator.ValidateOperation(1.5m, "+", 2.25m, 3.75m));
        Assert.True(_validator.ValidateOperation(6m, "/", 4m, 1.5m));
    }

    [Fact]
    public void Repeating_decimal_division_is_exact()
    {
        // 1 / 3 en decimal tiene precisión fija; el resultado propuesto debe coincidir exactamente.
        Assert.True(_validator.ValidateOperation(1m, "/", 3m, 1m / 3m));
        Assert.False(_validator.ValidateOperation(1m, "/", 3m, 0.33m));
    }

    [Theory]
    [InlineData("4 * 3 = 12", true)]
    [InlineData("4 * 3 = 15", false)]
    [InlineData("4*3=12", true)]
    [InlineData("2/4 = 1/2", true)]
    [InlineData("2/4 > 3/8", true)]
    [InlineData("3/8 > 2/4", false)]
    [InlineData("1/2 + 1/4 = 3/4", true)]
    [InlineData("2 + 3 = 5", true)]
    [InlineData("2 + 3 = 6", false)]
    [InlineData("10 - 4 = 6", true)]
    [InlineData("1/2 >= 1/2", true)]
    [InlineData("1/2 <= 3/4", true)]
    [InlineData("1/2 < 3/4", true)]
    [InlineData("1/2 == 2/4", true)]
    [InlineData("4 × 3 = 12", true)]
    [InlineData("4 x 3 = 12", true)]
    [InlineData("20 ÷ 4 = 5", true)]
    public void Validates_full_expression(string expression, bool expected)
    {
        Assert.Equal(expected, _validator.ValidateExpression(expression));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("4 * 3")]
    [InlineData("4 * 3 =")]
    [InlineData("= 12")]
    [InlineData("4 / 0 = 5")]
    [InlineData("2/0 = 0")]
    [InlineData("4 ** 3 = 12")]
    public void Invalid_expressions_return_false(string expression)
    {
        Assert.False(_validator.ValidateExpression(expression));
    }
}
