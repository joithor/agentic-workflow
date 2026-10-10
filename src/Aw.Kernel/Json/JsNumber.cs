using System.Globalization;

namespace Aw.Kernel.Json;

/// <summary>
/// Formats a double the way Node prints a number (ECMAScript Number::toString), starting from .NET's shortest
/// round-trip digits. Both runtimes pick the shortest digit string that round-trips, so only the layout differs:
/// plain digits up to 21 integer digits, '0.000…' down to 1e-6, exponent form ('1e+21', '1e-7') beyond.
/// </summary>
internal static class JsNumber
{
    internal const int MaxPlainExponent = 21;
    internal const int MinPlainExponent = -6;

    public static string Format(double value)
    {
        if (!double.IsFinite(value))
        {
            return "null";
        }

        if (value == 0)
        {
            return "0"; // -0 prints as 0 in JSON.stringify
        }

        var (digits, exponent) = Decompose(Math.Abs(value));
        var body = Layout(digits, exponent);
        return value < 0 ? "-" + body : body;
    }

    /// <summary>Splits a positive double into its significant digits d1..dk and n, where value = 0.d1..dk * 10^n.</summary>
    private static (string Digits, int Exponent) Decompose(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var e = text.IndexOf('E', StringComparison.Ordinal);
        var exponent = e < 0 ? 0 : int.Parse(text.AsSpan(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var mantissa = e < 0 ? text : text[..e];
        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var digits = dot < 0 ? mantissa : mantissa.Remove(dot, 1);
        var integerLength = dot < 0 ? mantissa.Length : dot;
        var leadingZeros = digits.Length - digits.TrimStart('0').Length;
        return (digits[leadingZeros..].TrimEnd('0'), integerLength - leadingZeros + exponent);
    }

    private static string Layout(string digits, int n)
    {
        var k = digits.Length;
        if (k <= n && n <= MaxPlainExponent)
        {
            return digits + new string('0', n - k);
        }

        if (n is > 0 and <= MaxPlainExponent)
        {
            return digits[..n] + "." + digits[n..];
        }

        if (n is > MinPlainExponent and <= 0)
        {
            return "0." + new string('0', -n) + digits;
        }

        var e = n - 1;
        var sign = e < 0 ? "-" : "+";
        var mantissa = k == 1 ? digits : digits[..1] + "." + digits[1..];
        return mantissa + "e" + sign + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
    }
}
