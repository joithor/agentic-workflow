using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class FailureExcerptTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_excerpt_starts_at_the_test_and_drops_durations_and_timestamps()
    {
        const string Log = "build ok\nfailed Spike.Eval_runs (23ms)\n  Assert.Equal() failure at 2026-10-15T04:05:06Z\n    Expected: 3\n    Actual: 4\n  at line\n  at line 2\nunrelated\n";

        var excerpt = FailureExcerpt.For(Log, "Eval_runs");

        Assert.Equal("failed Spike.Eval_runs\nAssert.Equal() failure at\nExpected: 3\nActual: 4\nat line", excerpt);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_failure_with_other_timings_compares_equal() =>
        Assert.Equal(
            FailureExcerpt.For("FAIL A.b (12 ms)\nboom after 1.5s", "A.b"),
            FailureExcerpt.For("FAIL A.b (900 ms)\nboom after 22s", "A.b"));

    [Fact, Trait("Lane", "unit")]
    public void A_log_that_never_names_the_test_falls_back_to_its_last_lines()
    {
        var excerpt = FailureExcerpt.For("a\nb\nc\nd\ne\nf\ng", "Nope");

        Assert.Equal("c\nd\ne\nf\ng", excerpt);
    }
}
