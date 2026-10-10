namespace Aw.Judge.Core.Json;

/// <summary>
/// A JSON value as JavaScript holds it after JSON.parse: the judge's CLI surface is frozen to the TS judge's
/// JSON.stringify output, so its JSON goes through this model (JsJson) rather than the Kernel profile.
/// </summary>
public abstract class JsValue
{
    private protected JsValue()
    {
    }

    /// <summary>The JSON null.</summary>
    public static JsValue Null { get; } = new JsNull();

    /// <summary>A string value.</summary>
    /// <param name="value">The UTF-16 text; lone surrogates are kept, as JavaScript keeps them.</param>
    /// <returns>The value.</returns>
    public static JsValue From(string value) => new JsString(value);

    /// <summary>A number value.</summary>
    /// <param name="value">The number; a non-finite one is written as null, as JSON.stringify writes it.</param>
    /// <returns>The value.</returns>
    public static JsValue From(double value) => new JsNumber(value);

    /// <summary>A boolean value.</summary>
    /// <param name="value">The boolean.</param>
    /// <returns>The value.</returns>
    public static JsValue From(bool value) => new JsBoolean(value);

    /// <summary>A string value, or null when <paramref name="value"/> is null.</summary>
    /// <param name="value">The text or null.</param>
    /// <returns>The value.</returns>
    public static JsValue FromNullable(string? value) => value is null ? Null : new JsString(value);
}
