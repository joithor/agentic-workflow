using Aw.Kernel.Testing;
using Aw.Sindri.Core.Health;

namespace Aw.Sindri.Tests.Health;

/// <summary>Workflow instances parked as unavailable in the health word (a Step's dependency stayed down through its retries): any make a healthy report degraded.</summary>
[Lane(TestLane.Unit)]
public sealed class HealthUnavailableTests
{
    private static readonly HealthReport _healthy = new(HealthWord.Healthy, [], Sentence: null);

    [Fact]
    public void None_parked_leaves_the_report_as_it_was() => Assert.Same(_healthy, HealthSummary.WithUnavailable(_healthy, 0));

    [Fact]
    public void Some_parked_make_a_healthy_report_degraded_with_a_problem_and_a_sentence()
    {
        var report = HealthSummary.WithUnavailable(_healthy, 2);

        Assert.Equal(HealthWord.Degraded, report.Word);
        var problem = Assert.Single(report.Problems);
        Assert.Equal("SND-ENGINE-003", problem.Code);
        Assert.Equal("2 workflow instances wait on a dependency that is down", problem.Clause);
        Assert.Equal("Sindri is degraded: 2 workflow instances wait on a dependency that is down.", report.Sentence);
    }

    [Fact]
    public void One_parked_instance_is_singular() =>
        Assert.Equal("1 workflow instance waits on a dependency that is down", Assert.Single(HealthSummary.WithUnavailable(_healthy, 1).Problems).Clause);

    [Fact]
    public void The_problem_text_says_how_to_run_them_again() =>
        Assert.Contains("sindri workflow answer <address> --resume", Assert.Single(HealthSummary.WithUnavailable(_healthy, 1).Problems).Text, StringComparison.Ordinal);

    [Theory]
    [InlineData(HealthWord.Stalled)]
    [InlineData(HealthWord.Rebuilding)]
    public void A_worse_word_stays_and_the_problem_joins_the_others(HealthWord word)
    {
        var worse = new HealthReport(word, [new HealthProblem("SND-VIEW-001", "x", "the views stopped")], "Sindri is worse: the views stopped.");

        var report = HealthSummary.WithUnavailable(worse, 3);

        Assert.Equal(word, report.Word);
        Assert.Equal(["SND-VIEW-001", "SND-ENGINE-003"], report.Problems.Select(static p => p.Code), StringComparer.Ordinal);
        Assert.Equal($"Sindri is {word.ToString().ToLowerInvariant()}: the views stopped and 3 workflow instances wait on a dependency that is down.", report.Sentence);
    }

    [Fact]
    public void Stuck_and_unavailable_instances_both_show_in_the_order_they_are_added()
    {
        var report = HealthSummary.WithUnavailable(HealthSummary.WithStuck(_healthy, 1), 1);

        Assert.Equal(["SND-ENGINE-002", "SND-ENGINE-003"], report.Problems.Select(static p => p.Code), StringComparer.Ordinal);
    }
}
