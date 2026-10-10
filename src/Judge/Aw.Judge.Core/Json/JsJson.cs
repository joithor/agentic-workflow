namespace Aw.Judge.Core.Json;

/// <summary>JSON.parse and JSON.stringify, byte for byte as the TS judge's node runtime does them.</summary>
public static class JsJson
{
    /// <summary>Parses JSON text as JSON.parse does.</summary>
    /// <param name="text">The text (a leading byte-order mark is not whitespace, so it fails, as in JavaScript).</param>
    /// <returns>The value, or null when JSON.parse would throw.</returns>
    public static JsValue? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return JsJsonReader.Read(text);
    }

    /// <summary>Writes a value as JSON.stringify(value) does.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The JSON text.</returns>
    public static string Stringify(JsValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsJsonWriter.Write(value, indented: false);
    }

    /// <summary>Writes a value as JSON.stringify(value, null, 2) does.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The indented JSON text, with no trailing newline.</returns>
    public static string StringifyIndented(JsValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsJsonWriter.Write(value, indented: true);
    }
}
