using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordRun;

internal sealed class RecordRunService(IStateStore store, IRunLedger ledger, IGit git, IFiles files, TimeProvider clock)
    : IService<RecordRunRequest, RecordRunResponse>
{
    public ValueTask<Result<RecordRunResponse>> Handle(RecordRunRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(state => Record(request, state)));

    private Result<RecordRunResponse> Record(RecordRunRequest request, BugFixState state)
    {
        var candidate = state.Require(Phase.Fix, Phase.Evaluate).Bind(_ => state.Find(request.Candidate)).Bind(Unrun);
        var run = candidate
            .Bind(_ => ledger.Read(request.Evidence))
            .Bind(stored => EvidenceRule.Accept(state, stored))
            .Bind(record => SameCheck(state.Check!, record)) // why: phase fix or evaluate comes after advance reproduce froze the check.
            .Bind(record => Against(candidate.Value, record))
            .Bind(record => Frozen(state.Check!, candidate.Value, record)); // why: as above.
        if (run.IsError)
        {
            return run.Error;
        }

        var passed = run.Value.Outcome == RunOutcome.Passed;
        var recorded = candidate.Value with { Run = new CandidateRun(request.Evidence, passed, clock.GetUtcNow()) };
        store.Save(request.StateDirectory, state.With(recorded).Step(Phase.Evaluate, $"record-run {recorded.Id.Value}", clock.GetUtcNow(), request.Evidence));
        return new RecordRunResponse(recorded.Id, passed, passed ? $"judge {recorded.Id.Value}" : "start-attempt with the failure reasons");
    }

    private static Result<Candidate> Unrun(Candidate candidate) =>
        candidate.Run is null ? candidate : BugFixErrors.RunRefused.With($"a run is already recorded for {candidate.Id.Value}");

    private static Result<RunRecord> SameCheck(FrozenCheck check, RunRecord record)
    {
        if (record.Kind != check.Kind)
        {
            return BugFixErrors.RunRefused.With($"the baseline check was a {Kind(check.Kind)}, this evidence is a {Kind(record.Kind)}");
        }

        if (record.Outcome == RunOutcome.Broken)
        {
            return BugFixErrors.RunRefused.With("the run has broken steps and no failed step; repair the selector and re-run, a broken run is not a verdict on the fix");
        }

        return record.Kind == CheckKind.Test && !(record.Command ?? []).SequenceEqual(check.Command ?? [], StringComparer.Ordinal)
            ? BugFixErrors.RunRefused.With($"the test command `{string.Join(' ', record.Command ?? [])}` differs from the baseline's `{string.Join(' ', check.Command ?? [])}`")
            : record;
    }

    private Result<RunRecord> Against(Candidate candidate, RunRecord record)
    {
        if (record.Commit != candidate.Commit)
        {
            return BugFixErrors.RunRefused.With($"the evidence ran against {record.Commit}, not {candidate.Id.Value}'s commit {candidate.Commit}");
        }

        var head = git.Head(candidate.Worktree);
        if (head.IsError || head.Value != candidate.Commit)
        {
            return head.IsError ? head.Error : BugFixErrors.RunRefused.With($"{candidate.Id.Value}'s branch moved to {head.Value} after record-candidate; record a new candidate");
        }

        return git.IsClean(candidate.Worktree).Bind<RunRecord>(clean => clean
            ? record
            : BugFixErrors.DirtyTree.With($"{candidate.Worktree} has uncommitted or untracked changes"));
    }

    // The frozen-check rule: a fix must not weaken the check, neither the file nor what the run executed.
    private Result<RunRecord> Frozen(FrozenCheck check, Candidate candidate, RunRecord record)
    {
        var current = git.Root(candidate.Worktree)
            .Bind(root => files.Sha256(files.FullPath(root, check.Path)))
            .Match(sha => sha, _ => string.Empty);
        return current == check.Sha256 && record.CheckSha256 == check.Sha256
            ? record
            : BugFixErrors.CheckNotFrozen.With($"the check file {check.Path} changed since the baseline");
    }

    private static string Kind(CheckKind kind) => kind == CheckKind.Test ? "test" : "ui-evidence script";
}
