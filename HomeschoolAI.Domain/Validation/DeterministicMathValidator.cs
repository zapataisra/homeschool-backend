using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace HomeschoolAI.Domain.Validation;

/// <summary>
/// Implementación determinista de <see cref="IMathValidator"/>.
/// <see cref="ValidateOperation"/> usa aritmética <c>decimal</c>;
/// <see cref="ValidateExpression"/> analiza la expresión completa con fracciones
/// exactas (numerador/denominador enteros y multiplicación cruzada) para evitar
/// errores de punto flotante en equivalencias como <c>2/4 = 1/2</c>.
/// </summary>
public sealed class DeterministicMathValidator : IMathValidator
{
    public bool ValidateOperation(decimal operand1, string operation, decimal operand2, decimal proposedResult)
    {
        var op = NormalizeOperation(operation);
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

    public bool ValidateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        try
        {
            var tokens = Tokenize(expression);
            if (tokens.Count == 0)
            {
                return false;
            }

            return new ExpressionParser(tokens).Evaluate();
        }
        catch (FormatException)
        {
            return false;
        }
        catch (DivideByZeroException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static readonly Regex BarraFractionRegex = new(@"\b(\d{1,6})\s*/\s*(\d{1,6})\b", RegexOptions.Compiled);
    private static readonly Regex EnDComerNRegex = new(@"(?:en\s+)?(\d{1,6})\s+y\s+(?:comer|comerse|tomar|tomarse|comerme|tomarme)\s+(\d{1,6})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RepartirXEntreYRegex = new(@"(?:repartir|dividir)\s+(\d{1,6})\s+entre\s+(\d{1,6})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NDeDPartesRegex = new(@"(\d{1,6})\s+de\s+(\d{1,6})\s+partes", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool? ValidateFractionEquivalence(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var fractions = ExtractFractions(response);
        if (fractions.Count < 2)
        {
            return null;
        }

        // Solo se invalida si el niño está AFIRMANDO que las fracciones son equivalentes.
        if (!ClaimsEquivalence(response))
        {
            return null;
        }

        // Denominador 0 → inválido.
        if (fractions.Any(f => f.Denominator == 0))
        {
            return false;
        }

        // Producto cruzado (exacto, sin punto flotante) entre todas las parejas.
        for (var i = 0; i < fractions.Count; i++)
        {
            for (var j = i + 1; j < fractions.Count; j++)
            {
                var a = fractions[i];
                var b = fractions[j];
                if (a.Numerator * b.Denominator != b.Numerator * a.Denominator)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<(long Numerator, long Denominator)> ExtractFractions(string text)
    {
        var fractions = new List<(long, long)>();

        // a/b con barra.
        foreach (Match match in BarraFractionRegex.Matches(text))
        {
            fractions.Add((long.Parse(match.Groups[1].Value), long.Parse(match.Groups[2].Value)));
        }

        // "dividirla en D y comer/tomar N" → N/D (numerador = N, denominador = D).
        foreach (Match match in EnDComerNRegex.Matches(text))
        {
            fractions.Add((long.Parse(match.Groups[2].Value), long.Parse(match.Groups[1].Value)));
        }

        // "repartir/dividir X entre Y" → X/Y.
        foreach (Match match in RepartirXEntreYRegex.Matches(text))
        {
            fractions.Add((long.Parse(match.Groups[1].Value), long.Parse(match.Groups[2].Value)));
        }

        // "N de D partes" → N/D.
        foreach (Match match in NDeDPartesRegex.Matches(text))
        {
            fractions.Add((long.Parse(match.Groups[1].Value), long.Parse(match.Groups[2].Value)));
        }

        return fractions;
    }

    private static bool ClaimsEquivalence(string text) =>
        text.Contains("igual", StringComparison.OrdinalIgnoreCase)
        || text.Contains("equivale", StringComparison.OrdinalIgnoreCase)
        || text.Contains("equivalente", StringComparison.OrdinalIgnoreCase)
        || text.Contains("lo mismo", StringComparison.OrdinalIgnoreCase)
        || text.Contains("o sea", StringComparison.OrdinalIgnoreCase)
        || text.Contains("osea", StringComparison.OrdinalIgnoreCase)
        || text.Contains("es decir", StringComparison.OrdinalIgnoreCase)
        || text.Contains('=');

    private static string? NormalizeOperation(string? operation)
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

    private enum TokenKind
    {
        Number,
        Plus,
        Minus,
        Multiply,
        Divide,
        Equal,
        Greater,
        Less,
        GreaterOrEqual,
        LessOrEqual,
        LeftParen,
        RightParen,
        End
    }

    private readonly record struct Token(TokenKind Kind, Fraction Value);

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var index = 0;

        while (index < input.Length)
        {
            var current = input[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (char.IsAsciiDigit(current) || current == '.')
            {
                var (value, nextIndex) = ReadNumber(input, index);
                tokens.Add(new Token(TokenKind.Number, value));
                index = nextIndex;
                continue;
            }

            switch (current)
            {
                case '+':
                    tokens.Add(new Token(TokenKind.Plus, Fraction.Zero));
                    index++;
                    break;
                case '-':
                    tokens.Add(new Token(TokenKind.Minus, Fraction.Zero));
                    index++;
                    break;
                case '*' or '×' or '·' or 'x' or 'X':
                    tokens.Add(new Token(TokenKind.Multiply, Fraction.Zero));
                    index++;
                    break;
                case '/' or '÷':
                    tokens.Add(new Token(TokenKind.Divide, Fraction.Zero));
                    index++;
                    break;
                case '(':
                    tokens.Add(new Token(TokenKind.LeftParen, Fraction.Zero));
                    index++;
                    break;
                case ')':
                    tokens.Add(new Token(TokenKind.RightParen, Fraction.Zero));
                    index++;
                    break;
                case '=':
                    if (index + 1 < input.Length && input[index + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.Equal, Fraction.Zero));
                        index += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Equal, Fraction.Zero));
                        index++;
                    }
                    break;
                case '>':
                    if (index + 1 < input.Length && input[index + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.GreaterOrEqual, Fraction.Zero));
                        index += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Greater, Fraction.Zero));
                        index++;
                    }
                    break;
                case '<':
                    if (index + 1 < input.Length && input[index + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.LessOrEqual, Fraction.Zero));
                        index += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Less, Fraction.Zero));
                        index++;
                    }
                    break;
                default:
                    throw new FormatException($"Carácter no válido en la expresión: '{current}'.");
            }
        }

        tokens.Add(new Token(TokenKind.End, Fraction.Zero));
        return tokens;
    }

    private static (Fraction Value, int NextIndex) ReadNumber(string input, int startIndex)
    {
        var integerDigits = new StringBuilder();
        var index = startIndex;

        while (index < input.Length && char.IsAsciiDigit(input[index]))
        {
            integerDigits.Append(input[index]);
            index++;
        }

        var fractionalDigits = new StringBuilder();
        if (index < input.Length && input[index] == '.')
        {
            index++;
            while (index < input.Length && char.IsAsciiDigit(input[index]))
            {
                fractionalDigits.Append(input[index]);
                index++;
            }
        }

        if (integerDigits.Length == 0 && fractionalDigits.Length == 0)
        {
            throw new FormatException("Número inválido en la expresión.");
        }

        var integerPart = integerDigits.Length == 0 ? "0" : integerDigits.ToString();
        var fractionalPart = fractionalDigits.ToString();

        var numerator = BigInteger.Parse(integerPart + fractionalPart);
        var denominator = BigInteger.Pow(10, fractionalPart.Length);

        return (Fraction.Create(numerator, denominator), index);
    }

    private readonly record struct Fraction(BigInteger Numerator, BigInteger Denominator)
    {
        public static readonly Fraction Zero = new(BigInteger.Zero, BigInteger.One);

        public static Fraction Create(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero)
            {
                throw new DivideByZeroException();
            }

            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            if (gcd > BigInteger.One)
            {
                numerator /= gcd;
                denominator /= gcd;
            }

            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            return new Fraction(numerator, denominator);
        }

        public static Fraction operator +(Fraction left, Fraction right) =>
            Create(left.Numerator * right.Denominator + right.Numerator * left.Denominator, left.Denominator * right.Denominator);

        public static Fraction operator -(Fraction left, Fraction right) =>
            Create(left.Numerator * right.Denominator - right.Numerator * left.Denominator, left.Denominator * right.Denominator);

        public static Fraction operator *(Fraction left, Fraction right) =>
            Create(left.Numerator * right.Numerator, left.Denominator * right.Denominator);

        public static Fraction operator /(Fraction left, Fraction right) =>
            Create(left.Numerator * right.Denominator, left.Denominator * right.Numerator);

        public static Fraction operator -(Fraction value) => new(-value.Numerator, value.Denominator);

        public int CompareTo(Fraction other) =>
            (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
    }

    private sealed class ExpressionParser
    {
        private readonly IReadOnlyList<Token> _tokens;
        private int _position;

        public ExpressionParser(IReadOnlyList<Token> tokens) => _tokens = tokens;

        public bool Evaluate()
        {
            var left = ParseSide();

            var comparison = Current;
            if (comparison.Kind is not (TokenKind.Equal or TokenKind.Greater or TokenKind.Less or TokenKind.GreaterOrEqual or TokenKind.LessOrEqual))
            {
                throw new FormatException("Falta un operador de comparación.");
            }

            Advance();
            var right = ParseSide();

            if (Current.Kind != TokenKind.End)
            {
                throw new FormatException("La expresión contiene símbolos inesperados.");
            }

            var comparisonResult = left.CompareTo(right);
            return comparison.Kind switch
            {
                TokenKind.Equal => comparisonResult == 0,
                TokenKind.Greater => comparisonResult > 0,
                TokenKind.Less => comparisonResult < 0,
                TokenKind.GreaterOrEqual => comparisonResult >= 0,
                TokenKind.LessOrEqual => comparisonResult <= 0,
                _ => false
            };
        }

        private Token Current => _tokens[_position];

        private void Advance() => _position++;

        private Fraction ParseSide()
        {
            var left = ParseTerm();

            while (Current.Kind is TokenKind.Plus or TokenKind.Minus)
            {
                var operation = Current;
                Advance();
                var right = ParseTerm();
                left = operation.Kind == TokenKind.Plus ? left + right : left - right;
            }

            return left;
        }

        private Fraction ParseTerm()
        {
            var left = ParseFactor();

            while (Current.Kind is TokenKind.Multiply or TokenKind.Divide)
            {
                var operation = Current;
                Advance();
                var right = ParseFactor();

                if (operation.Kind == TokenKind.Multiply)
                {
                    left *= right;
                }
                else
                {
                    if (right.Numerator.IsZero)
                    {
                        throw new DivideByZeroException();
                    }

                    left /= right;
                }
            }

            return left;
        }

        private Fraction ParseFactor()
        {
            var sign = 1;
            if (Current.Kind is TokenKind.Plus or TokenKind.Minus)
            {
                if (Current.Kind == TokenKind.Minus)
                {
                    sign = -1;
                }

                Advance();
            }

            Fraction value;
            if (Current.Kind == TokenKind.Number)
            {
                value = Current.Value;
                Advance();
            }
            else if (Current.Kind == TokenKind.LeftParen)
            {
                Advance();
                value = ParseSide();
                if (Current.Kind != TokenKind.RightParen)
                {
                    throw new FormatException("Falta cerrar el paréntesis.");
                }

                Advance();
            }
            else
            {
                throw new FormatException("Se esperaba un número o un paréntesis.");
            }

            return sign < 0 ? -value : value;
        }
    }
}
