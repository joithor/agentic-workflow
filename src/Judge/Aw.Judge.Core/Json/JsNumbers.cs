namespace Aw.Judge.Core.Json;

/// <summary>JavaScript's number-to-text and text-to-number conversions.</summary>
public static class JsNumbers
{
    /// <summary>Number.prototype.toString(): the shortest round-trip digits, exponent form below 1e-6 and from 1e21.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text, e.g. "0.85", "1", "1e-7", "NaN", "Infinity".</returns>
    public static string Format(double value) => throw new NotSupportedException("JsNumbers: make JsJsonTests pass");

    /// <summary>Number(text): trims whitespace, reads a decimal, 0x/0o/0b or Infinity literal; blank is 0.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or NaN when it isn't one.</returns>
    public static double FromString(string text) => throw new NotSupportedException("JsNumbers: make JsJsonTests pass");
}
