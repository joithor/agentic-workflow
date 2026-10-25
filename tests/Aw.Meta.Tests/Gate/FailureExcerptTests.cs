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

    private const string Suite = "config/hooks/tests/failopen.test.sh";
    private const string Fail = "FAIL: send-gate: judge missing \u2014 differs from the baseline (origin/main) hook";

    [Fact, Trait("Lane", "unit")]
    public void A_bash_suite_fails_the_same_way_in_the_runner_summary_and_in_its_raw_output()
    {
        var gate = $"PASS  ser  scripts/tests/a.test.sh  3s\nFAIL  ser  {Suite}  41s  exit 1\nPASS  ser  b.test.sh  1s\n\n"
            + $"FAIL  ser  {Suite}  (exit 1, 41s)\n      ! {Fail}\n      | PASS: codex adapter: hook script missing (rc=0)\n      | PASS: cursor adapter: hook exits 3 (rc=0)\n\nslowest suites:\n     41s  ser  {Suite}\n";
        var raw = $"heavy: waiting for the session lock (1 slot(s))\n{Fail}\n  current:  rc=0 out=x err=\n  baseline: rc=0 out=x err=<stage>/send-gate.sh: line N: printf: write error: Broken pipe\nPASS: send-gate: judge exits 1 (rc=0)\nPASS: x (562 ms vs 569 ms)\nPASS: y (rc=0)\nPASS: z (rc=0)\nPASS: w (rc=0)\nPASS: v (rc=0)\n";

        Assert.Equal(FailureExcerpt.For(gate, Suite), FailureExcerpt.For(raw, Suite));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bash_suite_failing_a_different_check_has_a_different_excerpt() =>
        Assert.False(string.Equals(
            FailureExcerpt.For($"{Fail}\nPASS: a\n", Suite),
            FailureExcerpt.For("FAIL: send-gate: judge garbage\nPASS: a\n", Suite),
            StringComparison.Ordinal));
}
