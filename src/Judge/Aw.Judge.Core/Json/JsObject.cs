using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Aw.Judge.Core.Json;

/// <summary>
/// A JSON object with JavaScript's property order: array-index keys ("0", "1", … up to 2^32 − 2) first in ascending
/// order, then the other keys in insertion order. Setting an existing key keeps its position (JSON.parse with a
/// duplicate key, an object spread, an assignment).
/// </summary>
public sealed class JsObject : JsValue
{
    private readonly Dictionary<string, JsValue> _values = new(StringComparer.Ordinal);
    private readonly List<string> _names = [];
    private readonly SortedList<uint, string> _indexes = [];

    /// <summary>How many properties it has.</summary>
    public int Count => _values.Count;

    /// <summary>The properties in JavaScript's order.</summary>
    public IEnumerable<KeyValuePair<string, JsValue>> Properties =>
        _indexes.Values.Concat(_names).Select(key => new KeyValuePair<string, JsValue>(key, _values[key]));

    /// <summary>A shallow copy of an object spread's source, as JavaScript's object spread copies it: an object's properties, an
    /// array's elements or a string's UTF-16 code units under their indexes, nothing for anything else.</summary>
    /// <param name="value">The spread source.</param>
    /// <returns>A new object.</returns>
    public static JsObject Spread(JsValue value)
    {
        var copy = new JsObject();
        switch (value)
        {
            case JsObject source:
                foreach (var (key, item) in source.Properties)
                {
                    _ = copy.Set(key, item);
                }

                break;
            case JsArray array:
                for (var i = 0; i < array.Items.Count; i++)
                {
                    _ = copy.Set(i.ToString(CultureInfo.InvariantCulture), array.Items[i]);
                }

                break;
            case JsString text:
                for (var i = 0; i < text.Value.Length; i++)
                {
                    _ = copy.Set(i.ToString(CultureInfo.InvariantCulture), From(text.Value[i].ToString()));
                }

                break;
            default:
                break;
        }

        return copy;
    }

    /// <summary>The value of an own property.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, when there is one.</param>
    /// <returns>Whether the object has the property.</returns>
    public bool TryGet(string key, [NotNullWhen(true)] out JsValue? value) => _values.TryGetValue(key, out value);

    /// <summary>Sets a property: an existing key keeps its position, a new one takes its place in JavaScript's order.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>This object.</returns>
    public JsObject Set(string key, JsValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        if (!_values.ContainsKey(key))
        {
            if (ArrayIndex(key) is { } index)
            {
                _indexes.Add(index, key);
            }
            else
            {
                _names.Add(key);
            }
        }

        _values[key] = value;
        return this;
    }

    /// <summary>Removes a property.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Whether it was there.</returns>
    public bool Remove(string key)
    {
        if (!_values.Remove(key))
        {
            return false;
        }

        _ = ArrayIndex(key) is { } index ? _indexes.Remove(index) : _names.Remove(key);
        return true;
    }

    // A canonical uint32 numeral below 2^32 − 1 (ECMAScript's array index), e.g. "0" or "42" but not "01".
    private static uint? ArrayIndex(string key)
    {
        var canonical = key.Length is > 0 and <= 10 && (key.Length == 1 || key[0] != '0') && key.All(char.IsAsciiDigit);
        return canonical && ulong.Parse(key, CultureInfo.InvariantCulture) is var n && n < uint.MaxValue ? (uint)n : null;
    }
}
