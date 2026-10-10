namespace Aw.Judge.Core.Json;

/// <summary>A JSON string, as UTF-16 code units (lone surrogates kept).</summary>
/// <param name="value">The text.</param>
public sealed class JsString(string value) : JsValue
{
    /// <summary>The text.</summary>
    public string Value { get; } = value;
}
