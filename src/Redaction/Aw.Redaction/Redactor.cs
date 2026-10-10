using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>The built-in pattern set (spec §4c "One pattern source").</summary>
public sealed class Redactor : IRedactor
{
    private const RegexOptions Always = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private readonly (string Id, Regex Regex)[] _rules;
    private readonly Regex _placeholder;

    /// <summary>A redactor over <paramref name="rules"/> (tests inject a slow rule to prove the timeout blocks).</summary>
    internal Redactor(IReadOnlyList<RedactionRule> rules, TimeSpan matchTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matchTimeout, TimeSpan.Zero);
        _rules = [.. rules.Select(r => (r.Id, new Regex(r.Pattern, r.Options | Always, matchTimeout)))];
        RuleIds = [.. rules.Select(r => r.Id).Distinct(StringComparer.Ordinal)];

        // Only a placeholder naming one of this redactor's rules is skipped, so a crafted "[REDACTED:<secret>]" is
        // still scanned.
        var ids = string.Join('|', RuleIds.Append("blocked").Select(Regex.Escape));
        _placeholder = new Regex(@"\[REDACTED:(?:" + ids + @")\]", Always, matchTimeout);
    }

    /// <summary>The per-pattern match timeout <see cref="Default"/> uses.</summary>
    public static TimeSpan DefaultMatchTimeout { get; } = TimeSpan.FromSeconds(1);

    /// <summary>A redactor over the built-in rules with <see cref="DefaultMatchTimeout"/>.</summary>
    public static Redactor Default { get; } = new(BuiltinRules.All, DefaultMatchTimeout);

    /// <summary>The rule ids, in priority order (an earlier rule names an overlapping hit).</summary>
    public IReadOnlyList<string> RuleIds { get; }

    /// <summary>Creates a redactor over the built-in rules with its own per-pattern match timeout.</summary>
    /// <param name="matchTimeout">The timeout for each pattern; must be positive.</param>
    /// <returns>The redactor.</returns>
    public static Redactor Create(TimeSpan matchTimeout) => new(BuiltinRules.All, matchTimeout);

    /// <inheritdoc/>
    public ScrubResult Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var hits = Merge(FindBoth(text))
                .Select(h => new RedactionHit(h.Rule, h.Start, h.End))
                .ToList();
            return hits.Count == 0
                ? new ScrubResult(text, [], ScrubOutcome.Clean, BlockCode: null)
                : new ScrubResult(Replace(text, hits), hits, ScrubOutcome.Redacted, BlockCode: null);
        }
        catch (RegexMatchTimeoutException)
        {
            // A timeout blocks; it never passes the text on (spec §4c). The caller logs the outcome.
            return new ScrubResult(ScrubResult.BlockedText, [], ScrubOutcome.Blocked, RedactionCodes.MatchTimeout);
        }
    }

    // The rules run on the text as written and on its folded copy, and the hits are united in original coordinates.
    // The folded pass sees through look-alikes, zero-width characters and JSON escapes; the plain pass keeps the
    // token boundaries the fold would change (Cyrillic prose glued to a token, a fullwidth letter before it, or a
    // Windows path whose backslash-r the fold would decode).
    private List<RawHit> FindBoth(string text)
    {
        var raw = Find(text);
        var folded = FoldedText.Fold(text);
        if (!string.Equals(folded.Text, text, StringComparison.Ordinal))
        {
            raw.AddRange(Find(folded.Text).Select(h =>
            {
                var (start, end) = folded.ToOriginal(h.Start, h.End);
                return h with { Start = start, End = end };
            }));
        }

        return raw;
    }

    private static string Replace(string text, List<RedactionHit> hits)
    {
        var output = new StringBuilder(text.Length);
        var at = 0;
        foreach (var hit in hits)
        {
            _ = output.Append(text, at, hit.Start - at).Append("[REDACTED:").Append(hit.Rule).Append(']');
            at = hit.End;
        }

        return output.Append(text, at, text.Length - at).ToString();
    }

    // Sorted by start, then rule priority; overlapping hits merge into the earlier one, which keeps its rule.
    private static List<RawHit> Merge(List<RawHit> raw)
    {
        raw.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Order.CompareTo(b.Order));
        var merged = new List<RawHit>(raw.Count);
        foreach (var hit in raw)
        {
            if (merged.Count > 0 && hit.Start < merged[^1].End)
            {
                merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, hit.End) };
            }
            else
            {
                merged.Add(hit);
            }
        }

        return merged;
    }

    private List<RawHit> Find(string text)
    {
        var placeholders = new Placeholders(_placeholder.Matches(text));
        var raw = new List<RawHit>();
        for (var order = 0; order < _rules.Length; order++)
        {
            var (id, regex) = _rules[order];
            foreach (Match match in regex.Matches(text))
            {
                var value = match.Groups["v"];
                var span = value.Success ? value : match;
                var end = span.Index + span.Length;
                if (span.Length > 0 && !placeholders.Contain(span.Index, end))
                {
                    raw.Add(new RawHit(id, span.Index, end, order));
                }
            }
        }

        return raw;
    }

    private readonly record struct RawHit(string Rule, int Start, int End, int Order);
}
