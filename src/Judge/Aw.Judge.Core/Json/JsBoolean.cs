namespace Aw.Judge.Core.Json;

/// <summary>A JSON true or false.</summary>
/// <param name="value">The boolean.</param>
public sealed class JsBoolean(bool value) : JsValue
{
    /// <summary>The boolean.</summary>
    public bool Value { get; } = value;
}
