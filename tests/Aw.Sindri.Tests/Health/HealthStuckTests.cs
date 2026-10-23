using Aw.Kernel.Testing;
using Aw.Sindri.Core.Health;

namespace Aw.Sindri.Tests.Health;

/// <summary>Stuck engine instances in the health word (API spec §3.8, E-30, P13): any make a healthy report degraded.</summary>
[Lane(TestLane.Unit)]
public sealed class HealthStuckTests
{
    private static readonly HealthReport _healthy = new(HealthWord.Healthy, [], Sentence: null);

    [Fact]
    public void None_stuck_leaves_the_report_as_it_was() => Assert.Same(_healthy, HealthSummary.WithStuck(_healthy, 0));

    [Fact]
    public void Some_stuck_make_a_healthy_report_degraded_with_a_problem_and_a_sentence()
    {
        var report = HealthSummary.WithStuck(_healthy, 2);

        Assert.Equal(HealthWord.Degraded, report.Word);
        var problem = Assert.Single(report.Problems);
        Assert.Equal("SND-ENGINE-002", problem.Code);
        Assert.Equal("2 workflow instances are stuck waiting", problem.Clause);
        Assert.Equal("Sindri is degraded: 2 workflow instances are stuck waiting.", report.Sentence);
    }

    [Fact]
    public void One_stuck_instance_is_singular() => Assert.Equal("1 workflow instance is stuck waiting", Assert.Single(HealthSummary.WithStuck(_healthy, 1).Problems).Clause);

    [Theory]
    [InlineData(HealthWord.Stalled)]
    [InlineData(HealthWord.Rebuilding)]
    public void A_worse_word_stays_and_the_stuck_problem_joins_the_others(HealthWord word)
    {
        var worse = new HealthReport(word, [new HealthProblem("SND-VIEW-001", "x", "the views stopped")], "Sindri is worse: the views stopped.");

        var report = HealthSummary.WithStuck(worse, 3);

        Assert.Equal(word, report.Word);
        Assert.Equal(["SND-VIEW-001", "SND-ENGINE-002"], report.Problems.Select(static p => p.Code), StringComparer.Ordinal);
        Assert.Equal($"Sindri is {word.ToString().ToLowerInvariant()}: the views stopped and 3 workflow instances are stuck waiting.", report.Sentence);
    }
}
