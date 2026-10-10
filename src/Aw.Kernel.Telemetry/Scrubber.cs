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
    /// its ToString(), cut at its first secret-named member ("Password = …", as records and anonymous objects print,
    /// nested ones included) so no part of that value or anything after it is written. Nesting deeper than
    /// <see cref="MaxDepth"/> (a list that contains itself) is written as "[REDACTED:too-deep]". Not regex-scrubbed.
    /// </summary>
    public string Render(object? value) => Render(value, out _);

    /// <summary>Whether <see cref="Render(object?)"/> withholds anything in <paramref name="value"/>.</summary>
    public bool Withholds(object? value)
    {
        _ = Render(value, out var withheld);
        return withheld;
    }

    private const int MaxDepth = 8;
    private const string TooDeepText = "[REDACTED:too-deep]";

    private string Render(object? value, out bool withheld)
    {
        withheld = false;
        return RenderAt(value, 0, ref withheld);
    }

    private string RenderAt(object? value, int depth, ref bool withheld)
    {
        if (depth > MaxDepth)
        {
            withheld = true;
            return TooDeepText;
        }

        return value switch
        {
            null => string.Empty,
            string text => text,
            char[] chars => new string(chars),
            IEnumerable items => RenderList(items, depth, ref withheld),
            _ => RenderObject(value, depth, ref withheld),
        };
    }

    private string RenderObject(object value, int depth, ref bool withheld) =>
        KeyedEntries.TryRead(value, out var key, out var item)
            ? Entry(key, item, depth + 1, ref withheld)
            : WithholdMembers(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, ref withheld);

    private string RenderList(IEnumerable items, int depth, ref bool withheld)
    {
        var parts = new List<string>();
        foreach (var (key, item, keyed) in KeyedEntries.Of(items))
        {
            parts.Add(keyed ? Entry(key, item, depth + 1, ref withheld) : RenderAt(item, depth + 1, ref withheld));
        }

        return string.Join(", ", parts);
    }

    // A KeyValuePair's value arrives as its text (KeyedEntries reads it without reflection), so that text is checked
    // too: as a nested "[key, value]" pair, then for secret-named members.
    private string Entry(string key, object? value, int depth, ref bool withheld)
    {
        if (IsSecretName(key))
        {
            withheld = true;
            return $"[{key}, {ScrubResult.SecretFieldText}]";
        }

        var text = value is string pairText && depth <= MaxDepth && KeyedEntries.TryReadText(pairText, out var innerKey, out var innerValue)
            ? Entry(innerKey, innerValue, depth + 1, ref withheld)
            : value is string plain ? WithholdMembers(plain, ref withheld) : RenderAt(value, depth, ref withheld);
        return $"[{key}, {text}]";
    }

    // Cut at the first secret-named "Name = " (fail closed: a value may hold ',' or '}', so where it ends can't be
    // known) and close the braces left open.
    private string WithholdMembers(string text, ref bool withheld)
    {
        try
        {
            foreach (Match member in Member.Matches(text))
            {
                if (!IsSecretName(member.Groups["name"].Value))
                {
                    continue;
                }

                withheld = true;
                var head = text[..(member.Index + member.Length)];
                return head + ScrubResult.SecretFieldText + Closers(head);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            withheld = true;
            return ScrubResult.BlockedText;
        }

        return text;
    }

    // The brackets left open in a cut text, closed innermost first: " }" for an object, "]" for a key-value pair, ")" for a tuple.
    private static string Closers(string head)
    {
        var open = new Stack<char>();
        foreach (var c in head)
        {
            if (c is '{' or '[' or '(')
            {
                open.Push(c);
            }
            else if (c is '}' or ']' or ')' && open.Count > 0)
            {
                _ = open.Pop();
            }
        }

        return string.Concat(open.Select(c => c switch { '{' => " }", '[' => "]", _ => ")" }));
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
            var text = keyed ? Entry(key, item, 1, ref withheld) : RenderAt(item, 1, ref withheld);
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

    // "Name = " after "{ " or ", ", as a record's or anonymous object's ToString() prints a member; or "[name, " /
    // "(name, " as a KeyValuePair or a tuple prints its key when it sits inside another object's text.
    [GeneratedRegex(@"(?<=[{,] )(?<name>[A-Za-z_][A-Za-z0-9_]*) = |(?<=[\[(])(?<name>[A-Za-z_][A-Za-z0-9_.-]*), ", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Member { get; }
}
