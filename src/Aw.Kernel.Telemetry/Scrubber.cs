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

    /// <summary>A named value as log text: withheld whole when the name is a secret's, otherwise its scrubbed text.</summary>
    public string ScrubField(string name, object? value, ref ScrubTally tally)
    {
        if (IsSecretName(name))
        {
            tally = tally.With(ScrubOutcome.Redacted);
            return ScrubResult.SecretFieldText;
        }

        return Scrub(Text(value), ref tally);
    }

    /// <summary>
    /// A named attribute value (a span or metric tag): withheld whole when the name is a secret's, whatever its type;
    /// numbers and flags kept; text scrubbed; each element of a list scrubbed (the result is a string array); anything
    /// else scrubbed as its text. When the tally stays clean the caller keeps the original value.
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
            IEnumerable items => ScrubElements(items, ref tally),
            _ => Scrub(Text(value), ref tally),
        };
    }

    /// <summary>Whether <paramref name="name"/> is a secret's name (password, api_key, …), ignoring a '@' or '$' template prefix.</summary>
    public bool IsSecretName(string name) => redactor.IsSecretName(name.TrimStart('@', '$'));

    /// <summary>The invariant text of a value; a list becomes its elements joined with ", ", as the log formatter writes it.</summary>
    public static string Text(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        IEnumerable items => string.Join(", ", items.Cast<object?>().Select(Text)),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private string[] ScrubElements(IEnumerable items, ref ScrubTally tally)
    {
        var scrubbed = new List<string>();
        foreach (var item in items)
        {
            scrubbed.Add(Scrub(Text(item), ref tally));
        }

        return [.. scrubbed];
    }

    private static bool IsNumberOrFlag(object value) =>
        value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
