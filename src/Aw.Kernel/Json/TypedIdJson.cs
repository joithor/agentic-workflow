using System.Globalization;
using System.Text.Json;

namespace Aw.Kernel.Json;

/// <summary>The typed-id converters' shared parsing and read errors. Refused values are not echoed: messages can reach HTTP responses.</summary>
internal static class TypedIdJson
{
    /// <summary>The error for a token that isn't <paramref name="expected"/>, or for a value the id type refuses.</summary>
    public static JsonException Failure<TId>(JsonTokenType found, JsonTokenType expected, string expectedText) =>
        found == expected ? Refused<TId>() : new($"Expected {expectedText} for {typeof(TId).Name}, found {found}.");

    /// <summary>The error for a value or dictionary key the id type refuses.</summary>
    public static JsonException Refused<TId>() => new($"The value is not a valid {typeof(TId).Name}.");

    /// <summary>
    /// Parses only the canonical form the converter writes: "D", lowercase. Uppercase is refused rather than
    /// normalized, so a record read and written back keeps its bytes (and any digest over them).
    /// </summary>
    public static bool TryParseGuid(string? text, out Guid value)
    {
        value = Guid.Empty;
        return text is not null && text.AsSpan().IndexOfAnyInRange('A', 'F') < 0 && Guid.TryParseExact(text, "D", out value);
    }

    /// <summary>Parses an int key in its canonical form: invariant digits, no '+', no leading zeros.</summary>
    public static bool TryParseInt32(string? text, out int value)
    {
        value = 0;
        return text is not null
            && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
            && string.Equals(value.ToString(CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
    }
}
