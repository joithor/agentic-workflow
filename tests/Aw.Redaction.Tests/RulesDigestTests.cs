using System.Text.RegularExpressions;

namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class RulesDigestTests
{
    [Fact]
    public void The_digest_follows_what_the_rules_match_not_their_ids_or_the_timeout()
    {
        var one = new Redactor([new RedactionRule("token", "tok_[a-z]{8}")], TimeSpan.FromSeconds(1)).RulesDigest;
        var widened = new Redactor([new RedactionRule("token", "tok_[a-z0-9]{8}")], TimeSpan.FromSeconds(1)).RulesDigest;
        var caseless = new Redactor([new RedactionRule("token", "tok_[a-z]{8}", RegexOptions.IgnoreCase)], TimeSpan.FromSeconds(1)).RulesDigest;
        var slower = new Redactor([new RedactionRule("token", "tok_[a-z]{8}")], TimeSpan.FromSeconds(2)).RulesDigest;

        Assert.Matches("^[0-9a-f]{64}$", one);
        Assert.Equal(3, new HashSet<string>([one, widened, caseless], StringComparer.Ordinal).Count);
        Assert.Equal(one, slower);
        Assert.Equal(Redactor.Default.RulesDigest, Redactor.Create(TimeSpan.FromSeconds(3)).RulesDigest);
        var extra = Redactor.Create([new ExtraPattern("acme-customer-id", "ACME-[0-9]{6}")], Redactor.DefaultMatchTimeout, TimeProvider.System);
        Assert.True(extra.IsValid);
        Assert.False(string.Equals(Redactor.Default.RulesDigest, extra.Redactor!.RulesDigest, StringComparison.Ordinal));   // why: IsValid holds, so a redactor was built
    }
}
