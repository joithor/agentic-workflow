using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class ScriptedJudgeContractTests : JudgeContract
{
    protected override IJudge Decides(JudgeDecision decision) => new ScriptedJudge().Answers(A.Verdict(decision));

    protected override IJudge Fails() => new ScriptedJudge().Answers(ScriptedJudge.Fails());
}
