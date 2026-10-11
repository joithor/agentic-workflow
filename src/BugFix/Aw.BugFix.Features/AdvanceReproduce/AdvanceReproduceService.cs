using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.AdvanceReproduce;

internal sealed class AdvanceReproduceService(IStateStore store, IRunLedger ledger, IFiles files, CheckLocator locator, WorkTree tree, TimeProvider clock)
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
            .Bind(record => AtHead(record, state, request.WorkingDirectory));
        var frozen = run.Bind(record => locator.Locate(request.WorkingDirectory, request.Check)
            .Bind(file => files.Sha256(file.FullPath).Bind(sha => Freeze(record, file, sha, Outputs(state, record)))));
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

    // Frozen from a clean checkout at the baseline commit: what the check names is decided by the commit, not the index.
    private Result<RunRecord> AtHead(RunRecord record, BugFixState state, string directory) =>
        tree.Clean(state, directory, "freeze the check from the clean baseline checkout").Bind<RunRecord>(checkout => checkout.Head == record.Commit
            ? record
            : BugFixErrors.NotReproduced.With($"the evidence ran against {record.Commit}, but {directory} is at {checkout.Head}"));

    private Result<FrozenCheck> Freeze(RunRecord record, CheckFile file, string sha, IReadOnlySet<string> outputs)
    {
        if (record.CheckSha256 != sha)
        {
            return BugFixErrors.CheckNotFrozen.With("the run executed a different version of the check file");
        }

        var root = file.FullPath[..^file.RepoPath.Length].TrimEnd('/');
        return CommandFiles(record, root, file, outputs).Map(commandFiles =>
            new FrozenCheck(record.Kind, file.RepoPath, sha, record.Command, record.Directory, record.ProgramSha256, record.Environment, commandFiles));
    }

    // What the baseline command's runs created in the baseline's work tree: the baseline run's own leftovers, and those
    // of earlier runs of the same command there (a re-run that writes the same report again leaves nothing new).
    private HashSet<string> Outputs(BugFixState state, RunRecord baseline) =>
        state.Runs.Select(run => ledger.Read(run.Evidence)).Where(static r => r.IsSuccess).Select(static r => r.Value.Record)
            .Where(r => r.Checkout == baseline.Checkout && r.Directory == baseline.Directory && (r.Command ?? []).SequenceEqual(baseline.Command ?? [], StringComparer.Ordinal))
            .Append(baseline)
            .SelectMany(static r => r.Leftovers.Keys)
            .ToHashSet(StringComparer.Ordinal);

    // The repository files the baseline command names besides the check (its runner, wrapper scripts, --require=,
    // --config=, -m modules): a fix that changes them changes what the check executes, so they are protected like it.
    // A script-like path that doesn't exist is refused: what isn't there now could be added by a fix.
    private Result<IReadOnlyList<string>> CommandFiles(RunRecord record, string root, CheckFile check, IReadOnlySet<string> outputs)
    {
        var directory = record.Directory.Length == 0 ? root : $"{root}/{record.Directory}";
        var found = new List<string>();
        foreach (var named in CommandFilesRule.Paths(record.Command ?? []))
        {
            // A file a run of this command created is its output (--junitxml=out/r.xml), not code a fix could change.
            var protect = Protected(named, directory);
            if (protect.IsError)
            {
                return protect.Error;
            }

            found.AddRange(protect.Value
                .Where(f => f.StartsWith(root + "/", StringComparison.Ordinal) && f != check.FullPath)
                .Select(f => f[(root.Length + 1)..])
                .Where(f => named.Specifier || !outputs.Contains(f)));
        }

        return Results.Success<IReadOnlyList<string>>([.. found.Distinct(StringComparer.Ordinal)]);
    }

    // A specifier is protected through every file the loader could open for it, whether or not it exists yet: a fix
    // that adds one the loader tries first (lib/setup before lib/setup.js) changes what runs.
    private Result<IEnumerable<string>> Protected((string Path, bool Required, bool Specifier) named, string directory)
    {
        var candidates = (named.Specifier ? CommandFilesRule.LoaderCandidates(named.Path) : [named.Path]).Select(c => files.FullPath(directory, c)).ToList();
        var existing = candidates.Select(files.RealPath).OfType<string>().ToList();
        if (existing.Count == 0 && named.Required && !files.Exists(files.FullPath(directory, named.Path)))
        {
            return BugFixErrors.CheckNotFrozen.With($"the baseline command names {named.Path}, which doesn't exist; a fix could add it, so name only files that exist");
        }

        return Results.Success(named.Specifier ? existing.Concat(candidates.Select(Path.GetFullPath)) : existing);
    }
}
