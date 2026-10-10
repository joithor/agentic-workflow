using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>The rules compiled with one match timeout, and the scan that finds their hits in one text.</summary>
internal sealed class CompiledRules
{
    private const RegexOptions Always = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private readonly (string Id, Regex Regex)[] _rules;
    private readonly Regex _placeholder;

    public CompiledRules(IReadOnlyList<RedactionRule> rules, IReadOnlyList<string> ruleIds, TimeSpan matchTimeout)
    {
        _rules = [.. rules.Select(r => (r.Id, new Regex(r.Pattern, r.Options | Always, matchTimeout)))];

        // Only a placeholder naming one of these rules is skipped, so a crafted "[REDACTED:<secret>]" is still scanned.
        var ids = string.Join('|', ruleIds.Append("blocked").Select(Regex.Escape));
        _placeholder = new Regex(@"\[REDACTED:(?:" + ids + @")\]", Always, matchTimeout);
    }

    /// <summary>Every rule's hits in <paramref name="text"/>, outside existing placeholders; unsorted, may overlap.</summary>
    /// <exception cref="RegexMatchTimeoutException">A pattern ran past the match timeout.</exception>
    public List<RawHit> Find(string text)
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
}
