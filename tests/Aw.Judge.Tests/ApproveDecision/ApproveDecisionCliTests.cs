using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ApproveDecision;

// The contract of 'judge approve' (TS runApprove: reason_code prefixed 'approved:'; unknown → exit 1 with a message).
public sealed class ApproveDecisionCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Approve_prints_the_row_with_the_reason_code_prefixed()
    {
        _ = await _state.DecisionLog().Record(A.Decision().WithId("d-1").Build(), CancellationToken.None);

        var run = await new JudgeCli(_state).Run("approve", "d-1");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        DecisionJson.AssertRow(run.Json(), "d-1", "approved:model", undone: false);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Approve_exits_1_for_an_unknown_or_blank_id()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "unknown decision: nope\n"), await cli.Run("approve", "nope"));
        Assert.Equal(new CliRun(1, "", "unknown decision: \n"), await cli.Run("approve"));
    }

    public void Dispose() => _state.Dispose();
}
