using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>The built-in pattern set (spec §4c "One pattern source").</summary>
public sealed class Redactor : IRedactor
{
    // The match timeout is a budget per window of input: a text of up to Window characters gets the redactor's
    // timeout, and each doubling of the text doubles it, so input size alone never blocks; only a pathological match
    // does (spec §4c). The rules are compiled once per size tier, on first use.
    private const int Window = 128 * 1024;

    private readonly IReadOnlyList<RedactionRule> _rules;
    private readonly TimeSpan _matchTimeout;
    private readonly CompiledRules?[] _tiers = new CompiledRules?[24];
    private readonly Regex _secretName;

    /// <summary>A redactor over <paramref name="rules"/> (tests inject a slow rule to prove the timeout blocks).</summary>
    internal Redactor(IReadOnlyList<RedactionRule> rules, TimeSpan matchTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matchTimeout, TimeSpan.Zero);
        _rules = rules;
        _matchTimeout = matchTimeout;
        RuleIds = [.. rules.Select(r => r.Id).Distinct(StringComparer.Ordinal)];
        _secretName = new Regex(
            BuiltinRules.SecretName,
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase,
            matchTimeout);
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
    public bool IsSecretName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _secretName.IsMatch(name);
    }

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
        var rules = RulesFor(text.Length);
        var raw = rules.Find(text);
        if (text.AsSpan().ContainsAnyExceptInRange('\u0000', '\u007F') || text.Contains('\\', StringComparison.Ordinal))
        {
            var folded = FoldedText.Fold(text);
            raw.AddRange(rules.Find(folded.Text).Select(h =>
            {
                var (start, end) = folded.ToOriginal(h.Start, h.End);
                return h with { Start = start, End = end };
            }));
        }

        return raw;
    }

    private CompiledRules RulesFor(int length)
    {
        var tier = 0;
        while (tier < _tiers.Length - 1 && (long)Window << tier < length)
        {
            tier++;
        }

        // Regex refuses a timeout past int.MaxValue - 1 ms (about 24.8 days); a longer budget is capped there.
        var timeout = TimeSpan.FromMilliseconds(Math.Min(_matchTimeout.TotalMilliseconds * (1L << tier), int.MaxValue - 1));
        return _tiers[tier] ??= new CompiledRules(_rules, RuleIds, timeout);
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
}
