namespace Aw.Judge.Core.Json;

/// <summary>A JSON number, held as JavaScript holds it: an IEEE double.</summary>
/// <param name="value">The number.</param>
public sealed class JsNumber(double value) : JsValue
{
    /// <summary>The number.</summary>
    public double Value { get; } = value;
}
