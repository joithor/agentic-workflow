using System.Text.RegularExpressions;

namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class RulesDigestTests
{
    [Fact]
    public void The_digest_follows_ids_patterns_and_options_not_the_timeout()
    {
        var one = new Redactor([new RedactionRule("token", "tok_[a-z]{8}")], TimeSpan.FromSeconds(1)).RulesDigest;
        var renamed = new Redactor([new RedactionRule("tokens", "tok_[a-z]{8}")], TimeSpan.FromSeconds(1)).RulesDigest;
        var widened = new Redactor([new RedactionRule("token", "tok_[a-z0-9]{8}")], TimeSpan.FromSeconds(1)).RulesDigest;
        var caseless = new Redactor([new RedactionRule("token", "tok_[a-z]{8}", RegexOptions.IgnoreCase)], TimeSpan.FromSeconds(1)).RulesDigest;
        var slower = new Redactor([new RedactionRule("token", "tok_[a-z]{8}")], TimeSpan.FromSeconds(2)).RulesDigest;

        Assert.Matches("^[0-9a-f]{64}$", one);
        Assert.Equal(4, new HashSet<string>([one, renamed, widened, caseless], StringComparer.Ordinal).Count);
        Assert.Equal(one, slower);
        Assert.Equal(Redactor.Default.RulesDigest, Redactor.Create(TimeSpan.FromSeconds(3)).RulesDigest);
        var extra = Redactor.Create([new ExtraPattern("acme-customer-id", "ACME-[0-9]{6}")], Redactor.DefaultMatchTimeout, TimeProvider.System);
        Assert.True(extra.IsValid);
        Assert.False(string.Equals(Redactor.Default.RulesDigest, extra.Redactor!.RulesDigest, StringComparison.Ordinal));   // why: IsValid holds, so a redactor was built
    }

    /// <summary>
    /// The scan engine's behaviour beyond the rules, pinned with its version: folded look-alikes, an escaped token, a
    /// zero-width split and two overlapping hits. A change to any of these outputs is a change to how text is scanned,
    /// so raise <see cref="Redactor.EngineVersion"/> (callers that store scrubbed text clean it again) and update this table.
    /// </summary>
    [Fact]
    public void Scan_behaviour_changes_raise_the_engine_version()
    {
        var token = "gh" + "p_" + new string('Q', 36);
        string[] samples =
        [
            "key " + token.Replace('Q', '\u0405') + " end",            // Cyrillic look-alike letters
            "key " + token.Replace("_", "\\u005f", StringComparison.Ordinal) + " end",   // a JSON escape
            "key " + token.Insert(10, "\u200B") + " end",              // a zero-width split
            "Authorization: Bearer " + token,                          // overlapping bearer and github-token hits
        ];

        var scanned = samples.Select(Redactor.Default.Scrub).Select(r => $"{r.Outcome}|{r.Text}|{string.Join(',', r.Hits.Select(h => h.Rule))}").ToList();
        string[] pinned =
        [
            "Redacted|key [REDACTED:github-token] end|github-token",
            "Redacted|key [REDACTED:github-token] end|github-token",
            "Redacted|key [REDACTED:github-token] end|github-token",
            "Redacted|Authorization: Bearer [REDACTED:github-token]|github-token",
        ];

        Assert.True(scanned.SequenceEqual(pinned, StringComparer.Ordinal),
            "scan behaviour changed: raise Redactor.EngineVersion and update this table (" + string.Join(" | ", scanned) + ")");
        Assert.Equal(1, Redactor.EngineVersion);   // raised together with the table above, never alone
    }
}
