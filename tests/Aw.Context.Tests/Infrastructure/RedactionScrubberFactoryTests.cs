using Aw.Context.Core;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Scrubbing;

namespace Aw.Context.Tests.Infrastructure;

public sealed class RedactionScrubberFactoryTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("acme-customer-id", "ACME-\\d{6}", "context.scrub.extraPatterns[0].pattern:")]
    [InlineData("acme-customer-id", "", "context.scrub.extraPatterns[0].pattern:")]
    [InlineData("acme-customer-id", "[A-Z]*", "context.scrub.extraPatterns[0].pattern:")]
    [InlineData("github-token", "ACME-[0-9]{6}", "context.scrub.extraPatterns[0].id:")]
    [InlineData("blocked", "ACME-[0-9]{6}", "context.scrub.extraPatterns[0].id:")]
    [InlineData(null, "ACME-[0-9]{6}", "context.scrub.extraPatterns[0].id:")]
    [InlineData("acme-customer-id", null, "context.scrub.extraPatterns[0].pattern:")]
    public void Redaction_refusals_are_001_at_the_key_path_and_never_quote_the_pattern(string? id, string? pattern, string path)
    {
        var refused = new RedactionScrubberFactory(TimeProvider.System).For([new ScrubPattern(id, pattern)]);

        Assert.Equal(ContextErrors.SettingsInvalid.Code, refused.Error.Code);
        Assert.StartsWith(path, refused.Error.Message, StringComparison.Ordinal);
        Assert.False(pattern is { Length: > 0 } && refused.Error.Message.Contains(pattern, StringComparison.Ordinal), "the message never quotes the pattern");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_list_reuses_the_last_build_and_another_rebuilds()
    {
        var factory = new RedactionScrubberFactory(TimeProvider.System);
        ScrubPattern[] acme = [new("acme-customer-id", "ACME-[0-9]{6}")];

        var first = factory.For(acme).Value;
        var again = factory.For([new("acme-customer-id", "ACME-[0-9]{6}")]).Value;
        var none = factory.For([]).Value;

        Assert.Same(first, again);
        Assert.NotEqual(first.Ruleset, none.Ruleset);
        Assert.Equal("order ACME-123456", none.Egress.Scrub("order ACME-123456").Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_fingerprint_tells_lists_apart_that_concatenate_alike() =>
        Assert.NotEqual(RedactionScrubberFactory.Fingerprint([new("a", "bc")]), RedactionScrubberFactory.Fingerprint([new("ab", "c")]));
}
