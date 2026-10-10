using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Redaction;

namespace Aw.Kernel.Telemetry;

/// <summary>Applies the redactor to telemetry text and values, recording in a tally whether anything was found or blocked.</summary>
internal sealed partial class Scrubber(IRedactor redactor)
{
    /// <summary>Scrubs <paramref name="text"/>; the tally learns whether it was redacted or blocked.</summary>
    public string Scrub(string text, ref ScrubTally tally)
    {
        var result = redactor.Scrub(text);
        tally = tally.With(result.Outcome);
        return result.Text;
    }

    /// <summary>A named value as log text: withheld whole when the name is a secret's, otherwise its scrubbed <see cref="Render(object?)"/>.</summary>
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
    /// numbers and flags kept; text (and a char array, as one string) scrubbed; a list becomes its scrubbed, rendered
    /// elements (a string array) whenever it holds anything but text, numbers and flags or anything was withheld;
    /// anything else becomes its scrubbed, rendered text. When the tally stays clean the caller keeps the original value.
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
            _ => ScrubRendered(value, ref tally),
        };
    }

    /// <summary>Whether <paramref name="name"/> is a secret's name (password, api_key, …), ignoring spaces and a '@' or '$' template prefix.</summary>
    public bool IsSecretName(string name) => redactor.IsSecretName(Bare(name));

    /// <summary>A template hole or field name without spaces and a '@' or '$' prefix.</summary>
    public static string Bare(string name) => name.Trim().TrimStart('@', '$');

    /// <summary>
    /// The invariant text of a value, as the log formatter writes it, with secret-named parts withheld: a list is its
    /// elements joined with ", "; a key-value entry is "[key, value]"; a char array is one string; any other object is
    /// its ToString() with secret-named members ("Password = …", as records and anonymous objects print) withheld.
    /// Not regex-scrubbed.
    /// </summary>
    public string Render(object? value) => Render(value, out _);

    /// <summary>Whether <see cref="Render(object?)"/> withholds anything in <paramref name="value"/>.</summary>
    public bool Withholds(object? value)
    {
        _ = Render(value, out var withheld);
        return withheld;
    }

    private string Render(object? value, out bool withheld)
    {
        withheld = false;
        switch (value)
        {
            case null:
                return string.Empty;
            case string text:
                return text;
            case char[] chars:
                return new string(chars);
            case IEnumerable items:
                var parts = new List<string>();
                foreach (var (key, item, keyed) in KeyedEntries.Of(items))
                {
                    parts.Add(keyed ? Entry(key, item, ref withheld) : RenderInto(item, ref withheld));
                }

                return string.Join(", ", parts);
            case not null when KeyedEntries.TryRead(value, out var key, out var item):
                return Entry(key, item, ref withheld);
            default:
                return WithholdMembers(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, ref withheld);
        }
    }

    private string RenderInto(object? value, ref bool withheld)
    {
        var text = Render(value, out var inner);
        withheld |= inner;
        return text;
    }

    private string Entry(string key, object? value, ref bool withheld)
    {
        if (IsSecretName(key))
        {
            withheld = true;
            return $"[{key}, {ScrubResult.SecretFieldText}]";
        }

        return $"[{key}, {RenderInto(value, ref withheld)}]";
    }

    private string WithholdMembers(string text, ref bool withheld)
    {
        var found = false;
        string result;
        try
        {
            result = Member.Replace(text, m =>
            {
                if (!IsSecretName(m.Groups["name"].Value))
                {
                    return m.Value;
                }

                found = true;
                return m.Groups["name"].Value + " = " + ScrubResult.SecretFieldText;
            });
        }
        catch (RegexMatchTimeoutException)
        {
            withheld = true;
            return ScrubResult.BlockedText;
        }

        withheld |= found;
        return result;
    }

    private string[] ScrubElements(IEnumerable items, ref ScrubTally tally)
    {
        var plain = true;
        var scrubbed = new List<string>();
        foreach (var item in items)
        {
            plain &= item is null or string || IsNumberOrFlag(item);
        }

        foreach (var (key, item, keyed) in KeyedEntries.Of(items))
        {
            var withheld = false;
            var text = keyed ? Entry(key, item, ref withheld) : RenderInto(item, ref withheld);
            plain &= !withheld && !keyed;
            scrubbed.Add(Scrub(text, ref tally));
        }

        if (!plain)
        {
            tally = tally.With(ScrubOutcome.Redacted);
        }

        return [.. scrubbed];
    }

    private string ScrubRendered(object value, ref ScrubTally tally)
    {
        if (Withholds(value))
        {
            tally = tally.With(ScrubOutcome.Redacted);
        }

        return Scrub(Render(value), ref tally);
    }

    private static bool IsNumberOrFlag(object value) =>
        value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    // "Name = value" up to the next ", " or " }", as a record's or anonymous object's ToString() prints its members.
    [GeneratedRegex(@"(?<=[{,] )(?<name>[A-Za-z_][A-Za-z0-9_]*) = (?<value>[^,}]*?)(?=, | })", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Member { get; }
}
