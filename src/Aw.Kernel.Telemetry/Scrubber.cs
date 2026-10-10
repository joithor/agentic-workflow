using System.Collections;
using System.Globalization;
using Aw.Redaction;

namespace Aw.Kernel.Telemetry;

/// <summary>Applies the redactor to telemetry text and values, recording in a tally whether anything was found or blocked.</summary>
internal sealed class Scrubber(IRedactor redactor)
{
    /// <summary>Scrubs <paramref name="text"/>; the tally learns whether it was redacted or blocked.</summary>
    public string Scrub(string text, ref ScrubTally tally)
    {
        var result = redactor.Scrub(text);
        tally = tally.With(result.Outcome);
        return result.Text;
    }

    /// <summary>A named value as log text: withheld whole when the name is a secret's, otherwise its scrubbed <see cref="Render"/>.</summary>
    public string ScrubField(string name, object? value, ref ScrubTally tally)
    {
        if (IsSecretName(name))
        {
            tally = tally.With(ScrubOutcome.Redacted);
            return ScrubResult.SecretFieldText;
        }

        return Scrub(Render(value), ref tally);
    }

    /// <summary>
    /// A named attribute value (a span or metric tag): withheld whole when the name is a secret's, whatever its type;
    /// numbers and flags kept; text (and a char array, as one string) scrubbed; each element of a list scrubbed, with a
    /// dictionary's or key-value list's secret-named entries withheld (the result is a string array); anything else
    /// scrubbed as its text. When the tally stays clean the caller keeps the original value.
    /// </summary>
    public object? ScrubValue(string name, object? value, ref ScrubTally tally)
    {
        if (IsSecretName(name))
        {
            tally = tally.With(ScrubOutcome.Redacted);
            return ScrubResult.SecretFieldText;
        }

        return value switch
        {
            null => null,
            _ when IsNumberOrFlag(value) => value,
            string text => Scrub(text, ref tally),
            char[] chars => Scrub(new string(chars), ref tally),
            IEnumerable items => ScrubElements(items, ref tally),
            _ => Scrub(Render(value), ref tally),
        };
    }

    /// <summary>Whether <paramref name="name"/> is a secret's name (password, api_key, …), ignoring a '@' or '$' template prefix.</summary>
    public bool IsSecretName(string name) => redactor.IsSecretName(name.TrimStart('@', '$'));

    /// <summary>
    /// The invariant text of a value, as the log formatter writes it, with secret-named entries withheld: a list is its
    /// elements joined with ", ", a key-value entry is "[key, value]", a char array is one string. Not regex-scrubbed.
    /// </summary>
    public string Render(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        char[] chars => new string(chars),
        IEnumerable items => string.Join(", ", Elements(items)),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    /// <summary>Whether a dictionary or key-value list holds an entry with a secret's name.</summary>
    public bool HasSecretEntries(object? value) =>
        value is IEnumerable items and not string and not char[] && Entries(items).Any(e => e.Keyed && IsSecretName(e.Key));

    private IEnumerable<string> Elements(IEnumerable items) =>
        Entries(items).Select(e => !e.Keyed ? Render(e.Value)
            : $"[{e.Key}, {(IsSecretName(e.Key) ? ScrubResult.SecretFieldText : Render(e.Value))}]");

    private string[] ScrubElements(IEnumerable items, ref ScrubTally tally)
    {
        if (HasSecretEntries(items))
        {
            tally = tally.With(ScrubOutcome.Redacted);
        }

        var scrubbed = new List<string>();
        foreach (var element in Elements(items))
        {
            scrubbed.Add(Scrub(element, ref tally));
        }

        return [.. scrubbed];
    }

    private static IEnumerable<(string Key, object? Value, bool Keyed)> Entries(IEnumerable items)
    {
        if (items is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return (Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty, entry.Value, true);
            }

            yield break;
        }

        foreach (var item in items)
        {
            yield return item switch
            {
                KeyValuePair<string, object?> pair => (pair.Key, pair.Value, true),
                KeyValuePair<string, string?> pair => (pair.Key, pair.Value, true),
                DictionaryEntry entry => (Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty, entry.Value, true),
                _ => (string.Empty, item, false),
            };
        }
    }

    private static bool IsNumberOrFlag(object value) =>
        value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
