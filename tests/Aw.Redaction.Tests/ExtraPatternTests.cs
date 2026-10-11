namespace Aw.Redaction.Tests;

/// <summary>Extra patterns from configuration (context spec D14, Phase 2): they scrub like built-in rules.</summary>
[Trait("Lane", "unit")]
public sealed class ExtraPatternTests
{
    private static ExtraPattern Acme => new("acme-customer-id", "ACME-[0-9]{6}");

    [Fact]
    public void A_configured_pattern_redacts_its_matches_as_its_own_id()
    {
        var redactor = Build(Acme);

        var result = redactor.Scrub("order ACME-123456 shipped");

        Assert.Equal("order [REDACTED:acme-customer-id] shipped", result.Text);
        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.Equal(new RedactionHit("acme-customer-id", 6, 17), Assert.Single(result.Hits));
    }

    [Fact]
    public void The_built_in_rules_still_apply_next_to_extra_patterns()
    {
        var redactor = Build(Acme);
        var input = Shapes.Join("ACME-123456 and gh", "p_", Shapes.Body, "Ab3dE6gH9");

        var result = redactor.Scrub(input);

        Assert.Equal("[REDACTED:acme-customer-id] and [REDACTED:github-token]", result.Text);
    }

    [Fact]
    public void Extra_ids_follow_the_built_in_ids_so_a_built_in_rule_names_an_overlapping_hit()
    {
        var greedy = new ExtraPattern("any-github-like", "gh[a-z]_[A-Za-z0-9]+");
        var redactor = Build(greedy);
        var input = Shapes.Join("gh", "p_", Shapes.Body, "Ab3dE6gH9");

        Assert.Equal("any-github-like", redactor.RuleIds[^1]);
        Assert.Equal("[REDACTED:github-token]", redactor.Scrub(input).Text);
    }

    [Fact]
    public void Scrubbing_twice_with_extra_patterns_changes_nothing_the_second_time()
    {
        // The pattern matches its own placeholder's text, so the second scrub is clean only because the placeholder of
        // an extra id is skipped.
        var redactor = Build(new ExtraPattern("ticket-ref", "(?i)ticket-[a-z0-9]+"));
        var once = redactor.Scrub("see TICKET-123456");

        var twice = redactor.Scrub(once.Text);

        Assert.Equal("see [REDACTED:ticket-ref]", once.Text);
        Assert.Equal(ScrubOutcome.Clean, twice.Outcome);
        Assert.Equal(once.Text, twice.Text);
    }

    [Fact]
    public void A_group_named_v_redacts_only_that_group()
    {
        var redactor = Build(new ExtraPattern("customer-ref", "customer=(?<v>[0-9]{6})"));

        var result = redactor.Scrub("customer=123456");

        Assert.Equal("customer=[REDACTED:customer-ref]", result.Text);
        Assert.Equal(new RedactionHit("customer-ref", 9, 15), Assert.Single(result.Hits));
    }

    [Fact]
    public void Extra_patterns_see_through_the_same_evasions_as_the_built_ins()
    {
        var redactor = Build(Acme);

        // A zero-width joiner and fullwidth digits inside the identifier.
        var result = redactor.Scrub("ACME-12\u200D3\uFF14\uFF15\uFF16");

        Assert.Equal("[REDACTED:acme-customer-id]", result.Text);
    }

    [Fact]
    public void Patterns_are_case_sensitive_unless_they_ask_otherwise()
    {
        Assert.Equal(ScrubOutcome.Clean, Build(Acme).Scrub("acme-123456").Outcome);
        Assert.Equal(
            "[REDACTED:acme-customer-id]",
            Build(new ExtraPattern("acme-customer-id", "(?i)ACME-[0-9]{6}")).Scrub("acme-123456").Text);
    }

    [Fact]
    public void Extra_patterns_keep_their_order_and_the_earlier_one_names_a_shared_start()
    {
        var redactor = Build(new ExtraPattern("short-ref", "ACME-[0-9]{3}"), new ExtraPattern("long-ref", "ACME-[0-9]{6}"));

        Assert.Equal(["short-ref", "long-ref"], redactor.RuleIds.TakeLast(2), StringComparer.Ordinal);
        Assert.Equal("[REDACTED:short-ref]", redactor.Scrub("ACME-123456").Text);
    }

    [Fact]
    public void Extra_patterns_scrub_text_past_the_first_size_tier()
    {
        var input = new string('x', 140_000) + " ACME-123456";

        var result = Build(Acme).Scrub(input);

        Assert.EndsWith(" [REDACTED:acme-customer-id]", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pattern_that_times_out_blocks_and_withholds_the_whole_text()
    {
        // 65 characters is the first size tier, so the budget is the 200 ms given; the built-in rules take well under
        // a millisecond on it, while the extra pattern backtracks for years: only the extra pattern can time out.
        var slow = new ExtraPattern("slow", "^(?:a|aa)+$");
        var redactor = Build(TimeSpan.FromMilliseconds(200), slow);

        var result = redactor.Scrub(new string('a', 64) + "!");

        Assert.Equal(ScrubOutcome.Blocked, result.Outcome);
        Assert.Equal(ScrubResult.BlockedText, result.Text);
        Assert.Equal(RedactionCodes.MatchTimeout, result.BlockCode);
        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_scrub_with_many_matches_blocks_once_it_runs_past_its_whole_scan_budget()
    {
        // Each match is fast, so no match times out; the clock steps 5 ms per reading and the budget is 4 x 10 ms.
        var clock = new SteppingClock(TimeSpan.FromMilliseconds(5));
        var created = Redactor.Create([new ExtraPattern("letter-a", "a")], TimeSpan.FromMilliseconds(10), clock);
        Assert.True(created.IsValid);

        var result = created.Redactor.Scrub(new string('a', 20));

        Assert.Equal(ScrubOutcome.Blocked, result.Outcome);
        Assert.Equal(ScrubResult.BlockedText, result.Text);
        Assert.Equal(RedactionCodes.MatchTimeout, result.BlockCode);
    }

    [Fact]
    public void A_scrub_within_its_whole_scan_budget_is_not_blocked()
    {
        var clock = new SteppingClock(TimeSpan.FromMilliseconds(5));
        var created = Redactor.Create([new ExtraPattern("letter-a", "a")], TimeSpan.FromMilliseconds(10), clock);
        Assert.True(created.IsValid);

        Assert.Equal("x [REDACTED:letter-a] y [REDACTED:letter-a]", created.Redactor.Scrub("x a y a").Text);
    }

    [Fact]
    public void No_extra_patterns_gives_the_built_in_rules()
    {
        var result = Redactor.Create([], Redactor.DefaultMatchTimeout, TimeProvider.System);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(Redactor.Default.RuleIds, result.Redactor.RuleIds);
    }

    [Fact]
    public void Create_with_extra_patterns_refuses_a_timeout_that_is_not_positive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Redactor.Create([Acme], TimeSpan.Zero, TimeProvider.System));

    // why: the null-forgiving operator passes null on purpose, to prove the guard.
    [Fact]
    public void A_null_pattern_list_is_a_bug() =>
        Assert.Throws<ArgumentNullException>(() => Redactor.Create(null!, Redactor.DefaultMatchTimeout, TimeProvider.System));

    // why: the null-forgiving operator passes null on purpose, to prove the guard.
    [Fact]
    public void A_null_clock_is_a_bug() =>
        Assert.Throws<ArgumentNullException>(() => Redactor.Create([Acme], Redactor.DefaultMatchTimeout, null!));

    private static Redactor Build(params ExtraPattern[] extras) => Build(Redactor.DefaultMatchTimeout, extras);

    private static Redactor Build(TimeSpan matchTimeout, params ExtraPattern[] extras)
    {
        var result = Redactor.Create(extras, matchTimeout, TimeProvider.System);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        return result.Redactor;
    }
}
