using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Readiness.App;
using Aw.Readiness.App.Commands;
using Aw.Readiness.App.Features.CheckRepo;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Criteria;
using Aw.Readiness.Core.Scoring;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Commands;

/// <summary>The report contracts, recorded from the C# build (spec §4c): the --json report (schema version 1) and
/// the text report, for a template missing CODEOWNERS with the host unreachable. A change to either is a reviewed
/// change to its golden file (set AW_GOLDEN_UPDATE=1 to rewrite).</summary>
[Lane(TestLane.Integration)]
public sealed class ReportGoldenTests
{
    private static CheckRepoResponse Report()
    {
        var findings = TemplateReadiness.Evaluate(
            A.ReadyTemplate().Without(".github/CODEOWNERS"), HostFactsFor.Scenario(HostScenario.Unreachable), RepoHostContract.Now);
        var card = ReadinessScoring.Score(findings);
        var since = RepoHostContract.Now.AddDays(-1);
        var delta = new RunDelta(since, 4, card.Level, [new StateChange(CriterionId.From("SEC-3"), CriterionState.Pass, CriterionState.Fail)]);
        return new CheckRepoResponse(CheckRepoResponse.CurrentSchemaVersion, RepoName.From("agentic-workflow"), "csharp-template",
            RepoHostContract.Now, new ReadinessTarget(0, 4), card.Level, card.Level >= 4, "<state>/readiness/runs/agentic-workflow/run.json",
            delta, card.Pillars, card.Tallies, findings);
    }

    [Fact]
    public void The_json_report_matches_its_golden_file() => Golden.Match(
        Encoding.UTF8.GetString(AwJson.Serialize(Report(), AwJson.CreateOptions(ReadinessJsonContext.Default, indented: true))) + "\n",
        typeof(ReportGoldenTests).Assembly, "Golden/report.json", Normalization.None);

    [Fact]
    public void The_text_report_matches_its_golden_file() => Golden.Match(
        ReportText.Render(Report()), typeof(ReportGoldenTests).Assembly, "Golden/report.txt", Normalization.None);
}
