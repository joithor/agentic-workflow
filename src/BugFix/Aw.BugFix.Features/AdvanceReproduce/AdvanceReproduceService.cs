using Aw.BugFix.Core.Errors;
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
        ValueTask.FromResult(store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Bind(state => Advance(request, state))));

    private Result<AdvanceReproduceResponse> Advance(AdvanceReproduceRequest request, BugFixState state)
    {
        var run = state.Require(Phase.Investigate)
            .Bind(_ => ledger.Read(request.Evidence))
            .Bind(stored => EvidenceRule.Accept(state, stored))
            .Bind(static record => record.AsBaseline())
            .Bind(record => AtHead(record, request.WorkingDirectory));
        var frozen = run.Bind(record => locator.Locate(request.WorkingDirectory, request.Check)
            .Bind(file => files.Sha256(file.FullPath).Bind(sha => Freeze(record, file, sha))));
        if (frozen.IsError)
        {
            return frozen.Error;
        }

        store.Save(
            request.StateDirectory,
            (state with { Check = frozen.Value, Baseline = new Baseline(request.Evidence, run.Value.Commit) })
                .Step(Phase.Reproduce, "advance reproduce", clock.GetUtcNow(), request.Evidence));
        return new AdvanceReproduceResponse(Phase.Reproduce, frozen.Value.Path, run.Value.Commit);
    }

    private Result<RunRecord> AtHead(RunRecord record, string directory) =>
        git.Head(directory).Bind<RunRecord>(head => head == record.Commit
            ? record
            : BugFixErrors.NotReproduced.With($"the evidence ran against {record.Commit}, but {directory} is at {head}"));

    private Result<FrozenCheck> Freeze(RunRecord record, CheckFile file, string sha)
    {
        if (record.CheckSha256 != sha)
        {
            return BugFixErrors.CheckNotFrozen.With("the run executed a different version of the check file");
        }

        var root = file.FullPath[..^file.RepoPath.Length].TrimEnd('/');
        return new FrozenCheck(record.Kind, file.RepoPath, sha, record.Command, record.Directory, CommandFiles(record, root, file));
    }

    // The repository files the baseline command names besides the check (its runner, wrapper scripts): a fix that changes
    // them changes what the check executes, so they are protected like the check.
    private List<string> CommandFiles(RunRecord record, string root, CheckFile check)
    {
        var directory = record.Directory.Length == 0 ? root : $"{root}/{record.Directory}";
        return [.. (record.Command ?? [])
            .Select(arg => files.RealPath(files.FullPath(directory, arg)))
            .OfType<string>()
            .Where(real => real.StartsWith(root + "/", StringComparison.Ordinal) && real != check.FullPath)
            .Select(real => real[(root.Length + 1)..])
            .Distinct(StringComparer.Ordinal)];
    }
}
