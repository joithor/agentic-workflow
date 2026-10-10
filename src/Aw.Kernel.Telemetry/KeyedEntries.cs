using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// Reads the key of a key-value shape without reflection, so a secret-named entry can be withheld: a dictionary's
/// entries, any <see cref="KeyValuePair{TKey, TValue}"/> (its key read from its "[key, value]" text), and a two-item
/// tuple whose first item is a string.
/// </summary>
internal static class KeyedEntries
{
    public static IEnumerable<(string Key, object? Value, bool Keyed)> Of(IEnumerable items)
    {
        if (items is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return (Text(entry.Key), entry.Value, true);
            }

            yield break;
        }

        foreach (var item in items)
        {
            yield return TryRead(item, out var key, out var value) ? (key, value, true) : (string.Empty, item, false);
        }
    }

    public static bool TryRead(object? item, out string key, out object? value)
    {
        switch (item)
        {
            case DictionaryEntry entry:
                (key, value) = (Text(entry.Key), entry.Value);
                return true;
            case ITuple { Length: 2 } tuple when tuple[0] is string first:
                (key, value) = (first, tuple[1]);
                return true;
            case not null when IsKeyValuePair(item):
                return FromText(item.ToString() ?? string.Empty, out key, out value);
            default:
                (key, value) = (string.Empty, null);
                return false;
        }
    }

    private static bool IsKeyValuePair(object item) =>
        item.GetType() is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);

    // KeyValuePair<,>.ToString() is "[key, value]".
    private static bool FromText(string text, out string key, out object? value)
    {
        var comma = text.IndexOf(", ", StringComparison.Ordinal);
        if (text.Length < 2 || text[0] != '[' || text[^1] != ']' || comma < 1)
        {
            (key, value) = (string.Empty, text);
            return false;
        }

        (key, value) = (text[1..comma], text[(comma + 2)..^1]);
        return true;
    }

    private static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
