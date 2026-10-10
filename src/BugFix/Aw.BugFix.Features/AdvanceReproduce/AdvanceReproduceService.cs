using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.AdvanceReproduce;

internal sealed class AdvanceReproduceService(IStateStore store, IRunLedger ledger, IGit git, IFiles files, CheckLocator locator, TimeProvider clock)
    : IService<AdvanceReproduceRequest, AdvanceReproduceResponse>
{
    public ValueTask<Result<AdvanceReproduceResponse>> Handle(AdvanceReproduceRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(state => Advance(request, state)));

    private Result<AdvanceReproduceResponse> Advance(AdvanceReproduceRequest request, BugFixState state)
    {
        var run = state.Require(Phase.Investigate)
            .Bind(_ => ledger.Read(request.Evidence))
            .Bind(stored => EvidenceRule.Accept(state, stored))
            .Bind(Reproduces)
            .Bind(record => AtHead(record, request.WorkingDirectory));
        if (run.IsError)
        {
            return run.Error;
        }

        var check = locator.Locate(request.WorkingDirectory, request.Check)
            .Bind(file => files.Sha256(file.FullPath).Bind(sha => Executed(run.Value, file, sha)));
        if (check.IsError)
        {
            return check.Error;
        }

        var frozen = check.Value;
        store.Save(
            request.StateDirectory,
            (state with { Check = frozen, Baseline = new Baseline(request.Evidence, run.Value.Commit) })
                .Step(Phase.Reproduce, "advance reproduce", clock.GetUtcNow(), request.Evidence));
        return new AdvanceReproduceResponse(Phase.Reproduce, frozen.Path, run.Value.Commit);
    }

    // A check that already passes can never prove a fix, and a broken selector is not a reproduction.
    private static Result<RunRecord> Reproduces(RunRecord record) => record.Outcome switch
    {
        RunOutcome.Failed => record,
        RunOutcome.Passed => BugFixErrors.NotReproduced.With(
            "the baseline check passed on the unfixed code; the bug is not reproduced, so this check cannot prove a fix"),
        RunOutcome.Broken => BugFixErrors.NotReproduced.With(
            "the baseline run has broken steps and no failed step; a broken selector is not a reproduction, so repair the script and re-run"),
        _ => throw new ArgumentOutOfRangeException(nameof(record), record.Outcome, "not an outcome"),
    };

    private Result<RunRecord> AtHead(RunRecord record, string directory) =>
        git.Head(directory).Bind<RunRecord>(head => head == record.Commit
            ? record
            : BugFixErrors.NotReproduced.With($"the evidence ran against {record.Commit}, but {directory} is at {head}"));

    private static Result<FrozenCheck> Executed(RunRecord record, CheckFile file, string sha) =>
        record.CheckSha256 == sha
            ? new FrozenCheck(record.Kind, file.RepoPath, sha, record.Command)
            : BugFixErrors.CheckNotFrozen.With("the run executed a different version of the check file");
}
