namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class ScrubContractTests
{
    [Fact]
    public void Scrubbing_twice_changes_nothing_the_second_time()
    {
        var input = Shapes.Join("API", "_KEY=", Shapes.Body, "Ab37 and gh", "p_", Shapes.Body, "Ab3dE6gH9");
        var once = Redactor.Default.Scrub(input);

        var twice = Redactor.Default.Scrub(once.Text);

        Assert.Equal(once.Text, twice.Text);
        Assert.Equal(ScrubOutcome.Clean, twice.Outcome);
    }

    [Fact]
    public void A_crafted_placeholder_holding_a_secret_is_still_scanned()
    {
        var input = Shapes.Join("[REDACTED:sk-", "ant-", Shapes.Body, Shapes.Body, "]");

        var result = Redactor.Default.Scrub(input);

        Assert.DoesNotContain(Shapes.Tail, result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hits_name_rules_and_spans_but_never_the_value()
    {
        var input = Shapes.Join("x ", "sk", "_live_", Shapes.Body, " y");

        var hit = Assert.Single(Redactor.Default.Scrub(input).Hits);

        Assert.Equal(new RedactionHit("stripe-key", 2, input.Length - 2), hit);
        Assert.DoesNotContain(Shapes.Tail, hit.ToString(), StringComparison.Ordinal);
    }

    // why: the null-forgiving operator passes null on purpose, to prove the guard.
    [Fact]
    public void Null_text_is_a_bug() =>
        Assert.Throws<ArgumentNullException>(() => Redactor.Default.Scrub(null!));

    [Fact]
    public void The_rule_ids_are_listed_in_priority_order()
    {
        var ids = Redactor.Default.RuleIds.ToList();

        Assert.Equal("private-key", ids[0]);
        Assert.Contains("secret-assignment", ids, StringComparer.Ordinal);
        Assert.True(
            ids.IndexOf("anthropic-key") < ids.IndexOf("openai-key"),
            "anthropic-key must outrank openai-key, which also matches sk-ant-");
    }

    [Fact]
    public void Create_refuses_a_timeout_that_is_not_positive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Redactor.Create(TimeSpan.Zero));
}
