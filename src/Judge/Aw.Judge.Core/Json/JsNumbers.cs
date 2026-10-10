using System.Globalization;

namespace Aw.Judge.Core.Json;

/// <summary>JavaScript's number-to-text and text-to-number conversions.</summary>
public static class JsNumbers
{
    /// <summary>Number.prototype.toString(): the shortest round-trip digits, exponent form below 1e-6 and from 1e21.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text, e.g. "0.85", "1", "1e-7", "NaN", "Infinity".</returns>
    public static string Format(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (value == 0 || double.IsInfinity(value))
        {
            return value == 0 ? "0" : value > 0 ? "Infinity" : "-Infinity";
        }

        if (value < 0)
        {
            return "-" + Format(-value);
        }

        var (digits, point) = Decompose(value);
        return Layout(digits, point);
    }

    /// <summary>Number(text): trims whitespace, reads a decimal, 0x/0o/0b or Infinity literal; blank is 0.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or NaN when it isn't one.</returns>
    public static double FromString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var t = Trim(text);
        if (t.Length == 0)
        {
            return 0;
        }

        if (string.Equals(t.TrimStart('+', '-'), "Infinity", StringComparison.Ordinal) && t.Length <= "+Infinity".Length)
        {
            return t[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (t.Length > 2 && t[0] == '0' && Radix(t[1]) is { } radix)
        {
            return Integer(t[2..], radix);
        }

        return IsDecimalLiteral(t) ? double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture) : double.NaN;
    }

    // The shortest round-trip digits (no leading or trailing zeros) and the decimal point's position n:
    // value = 0.<digits> × 10^n.
    private static (string Digits, int Point) Decompose(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var e = text.IndexOf('E', StringComparison.Ordinal);
        var mantissa = e < 0 ? text : text[..e];
        var exponent = e < 0 ? 0 : int.Parse(text[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var all = mantissa.Replace(".", "", StringComparison.Ordinal);
        var leading = all.Length - all.TrimStart('0').Length;
        return (all.Trim('0'), (dot < 0 ? mantissa.Length : dot) - leading + exponent);
    }

    // ECMAScript Number::toString with k digits and point n.
    private static string Layout(string digits, int n)
    {
        var k = digits.Length;
        if (k <= n && n <= 21)
        {
            return digits + new string('0', n - k);
        }

        if (n is > 0 and <= 21)
        {
            return digits[..n] + "." + digits[n..];
        }

        if (n is > -6 and <= 0)
        {
            return "0." + new string('0', -n) + digits;
        }

        var exponent = n - 1;
        var mantissa = k == 1 ? digits : digits[..1] + "." + digits[1..];
        return mantissa + "e" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString(CultureInfo.InvariantCulture);
    }

    private static int? Radix(char prefix) => char.ToLowerInvariant(prefix) switch
    {
        'x' => 16,
        'o' => 8,
        'b' => 2,
        _ => null,
    };

    private static double Integer(string digits, int radix)
    {
        var value = 0d;
        foreach (var c in digits)
        {
            var digit = char.IsAsciiDigit(c) ? c - '0' : char.IsAsciiLetter(c) ? char.ToLowerInvariant(c) - 'a' + 10 : radix;
            if (digit >= radix)
            {
                return double.NaN;
            }

            value = (value * radix) + digit;
        }

        return value;
    }

    // JavaScript's WhiteSpace and LineTerminator: .NET's white space without U+0085, plus U+FEFF.
    private static string Trim(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsSpace(text[start]))
        {
            start++;
        }

        while (end > start && IsSpace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }

    private static bool IsSpace(char c) => c == '\uFEFF' || (c != '\u0085' && char.IsWhiteSpace(c));

    // StrDecimalLiteral: [+-] (digits [. digits] | . digits) [e [+-] digits], with at least one mantissa digit.
    private static bool IsDecimalLiteral(string t)
    {
        var i = t[0] is '+' or '-' ? 1 : 0;
        var whole = Digits(t, ref i);
        var fraction = 0;
        if (i < t.Length && t[i] == '.')
        {
            i++;
            fraction = Digits(t, ref i);
        }

        if (whole + fraction == 0)
        {
            return false;
        }

        if (i < t.Length && t[i] is 'e' or 'E')
        {
            i += i + 1 < t.Length && t[i + 1] is '+' or '-' ? 2 : 1;
            return Digits(t, ref i) > 0 && i == t.Length;
        }

        return i == t.Length;
    }

    private static int Digits(string t, ref int i)
    {
        var start = i;
        while (i < t.Length && char.IsAsciiDigit(t[i]))
        {
            i++;
        }

        return i - start;
    }
}
