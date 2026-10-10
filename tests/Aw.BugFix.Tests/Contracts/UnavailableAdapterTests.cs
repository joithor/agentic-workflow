using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Infrastructure;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class UnavailableAdapterTests
{
    [Fact]
    public void Until_their_CLI_contracts_exist_judge_and_ui_evidence_answer_unavailable_and_record_nothing()
    {
        var input = new ResolutionCheckInput("b", "e", "a", "r", CheckKind.Test, "s", BeforePassed: false, AfterPassed: true, "stat", Diff: null);

        Assert.Equal(BugFixErrors.JudgeUnavailable.Code, new UnavailableJudge().ResolutionCheck(input).Error.Code);
        Assert.Equal(BugFixErrors.UiEvidenceUnavailable.Code, new UnavailableUiEvidence().Run(new UiRunRequest("/s", "/r", "c", "/w", "/l")).Error.Code);
    }
}
