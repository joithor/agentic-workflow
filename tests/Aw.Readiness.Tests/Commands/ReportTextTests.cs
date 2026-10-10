using Aw.Readiness.App.Commands;
using Aw.Readiness.App.Features.CheckRepo;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Criteria;
using Aw.Readiness.Core.Scoring;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Commands;

public sealed class ReportTextTests
{
    private static CheckRepoResponse Report(RunDelta delta, bool unknownHost = false)
    {
        var host = HostFactsFor.Scenario(unknownHost ? HostScenario.Unreachable : HostScenario.Ready);
        var findings = TemplateReadiness.Evaluate(A.ReadyTemplate().Without(".github/CODEOWNERS"), host, RepoHostContract.Now);
        var card = ReadinessScoring.Score(findings);
        return new CheckRepoResponse(CheckRepoResponse.CurrentSchemaVersion, RepoName.From("agentic-workflow"), "csharp-template",
            RepoHostContract.Now, new ReadinessTarget(0, 4), card.Level, card.Level >= 4, [], "memory:1", delta, card.Pillars, card.Tallies, findings);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_text_names_the_level_target_and_each_state_in_words()
    {
        var text = ReportText.Render(Report(new RunDelta(Since: null, LevelBefore: null, 5, [])));

        Assert.Contains("Level 5 of 5", text, StringComparison.Ordinal);
        Assert.Contains("target 4 (ring 0): holds", text, StringComparison.Ordinal);
        Assert.Contains("first run: baseline recorded (memory:1)", text, StringComparison.Ordinal);
        Assert.Contains("FAIL  SEC-3", text, StringComparison.Ordinal);
        Assert.Contains("fix: add CODEOWNERS", text, StringComparison.Ordinal);
        Assert.Contains("pass  SV-1", text, StringComparison.Ordinal);
        Assert.Contains("Security", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', text);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unconfirmed_target_names_the_unread_criteria()
    {
        var report = Report(new RunDelta(Since: null, LevelBefore: null, 5, []), unknownHost: true) with
        {
            Holds = false,
            Unconfirmed = [CriterionId.From("SEC-1"), CriterionId.From("SEC-2")],
        };

        var text = ReportText.Render(report);

        Assert.Contains("target 4 (ring 0): does not hold; SEC-1, SEC-2 weren't read and could raise the level", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_delta_lists_each_change_and_the_level_move()
    {
        var since = RepoHostContract.Now.AddDays(-1);
        var delta = new RunDelta(since, 4, 5, [new StateChange(CriterionId.From("SEC-3"), CriterionState.Pass, CriterionState.Fail)]);

        var text = ReportText.Render(Report(delta));

        Assert.Contains("since 2026-10-09T12:00:00Z: level 4 -> 5, 1 change", text, StringComparison.Ordinal);
        Assert.Contains("SEC-3: pass -> fail", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_level_move_without_a_compared_change_is_reported_as_a_move()
    {
        var text = ReportText.Render(Report(new RunDelta(RepoHostContract.Now.AddDays(-1), 3, 4, [])));

        Assert.Contains("level 3 -> 4, no compared change (criteria unread on either run aren't compared)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("no change (level", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unchanged_run_says_so_and_unknowns_are_words()
    {
        var text = ReportText.Render(Report(new RunDelta(RepoHostContract.Now.AddDays(-1), 4, 4, []), unknownHost: true));

        Assert.Contains("no change", text, StringComparison.Ordinal);
        Assert.Contains("unknown  SEC-1", text, StringComparison.Ordinal);
    }
}
