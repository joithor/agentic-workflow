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
        return CommandFiles(record, root, file).Map(commandFiles =>
            new FrozenCheck(record.Kind, file.RepoPath, sha, record.Command, record.Directory, record.ProgramSha256, record.Environment, commandFiles));
    }

    // The repository files the baseline command names besides the check (its runner, wrapper scripts, --require=,
    // --config=, -m modules): a fix that changes them changes what the check executes, so they are protected like it.
    // A script-like path that doesn't exist is refused: what isn't there now could be added by a fix.
    private Result<IReadOnlyList<string>> CommandFiles(RunRecord record, string root, CheckFile check)
    {
        var directory = record.Directory.Length == 0 ? root : $"{root}/{record.Directory}";
        var found = new List<string>();
        foreach (var (path, required, specifier) in CommandFilesRule.Paths(record.Command ?? []))
        {
            // A specifier is protected through every file the loader could open for it, whether or not it exists yet: a
            // fix that adds one the loader tries first (lib/setup before lib/setup.js) changes what runs.
            var candidates = (specifier ? CommandFilesRule.LoaderCandidates(path) : [path]).Select(c => files.FullPath(directory, c)).ToList();
            var existing = candidates.Select(files.RealPath).OfType<string>().ToList();
            if (existing.Count == 0 && required && !files.Exists(files.FullPath(directory, path)))
            {
                return BugFixErrors.CheckNotFrozen.With($"the baseline command names {path}, which doesn't exist; a fix could add it, so name only files that exist");
            }

            var protect = specifier ? existing.Concat(candidates.Select(Path.GetFullPath)) : existing;
            found.AddRange(protect
                .Where(f => f.StartsWith(root + "/", StringComparison.Ordinal) && f != check.FullPath)
                .Select(f => f[(root.Length + 1)..]));
        }

        return Results.Success<IReadOnlyList<string>>([.. found.Distinct(StringComparer.Ordinal)]);
    }
}
