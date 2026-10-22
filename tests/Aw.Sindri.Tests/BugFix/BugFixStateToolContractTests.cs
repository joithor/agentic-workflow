using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.BugFix;

namespace Aw.Sindri.Tests.BugFix;

/// <summary>The real bugfix-state, through the adapter, honours the port's contract the in-memory fake does; its judge is unavailable until Judge's CLI contract is published, which the last test pins.</summary>
[Lane(TestLane.Integration)]
public sealed class BugFixStateToolContractTests : BugFixStateContract, IDisposable
{
    private readonly RealBugFixState _real = new();

    protected override string StateDirectory => _real.State;

    protected override string EmptyDirectory => _real.Empty;

    protected override FrozenCheck Check => RealBugFixState.FrozenCheck;

    public void Dispose() => _real.Dispose();

    protected override IBugFixState Create() => _real.Tool();

    protected override CandidateSource Commit(bool fixes) => _real.Commit(fixes);

    [Fact]
    public async Task Judge_is_refused_as_unavailable_by_bugfix_state_until_the_Judge_adapter_lands()
    {
        var port = Create();
        var source = Commit(fixes: true);
        _ = await port.StartAttempt(StateDirectory, AttemptMode.Whole, TestContext.Current.CancellationToken);
        _ = await port.RecordCandidate(StateDirectory, source, TestContext.Current.CancellationToken);
        _ = await port.Recheck(StateDirectory, "c1", source.Worktree, Check, TestContext.Current.CancellationToken);

        var verdict = await port.Judge(StateDirectory, "c1", TestContext.Current.CancellationToken);

        Expect.Equal(BugFixAnswerStatus.Refused, verdict.Status);
        Expect.Equal("AW-BUGFIX-016", verdict.Refusal!.Code);
    }
}
