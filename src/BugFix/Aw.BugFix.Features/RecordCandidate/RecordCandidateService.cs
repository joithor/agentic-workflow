using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordCandidate;

internal sealed class RecordCandidateService(IStateStore store, IGit git, TimeProvider clock) : IService<RecordCandidateRequest, RecordCandidateResponse>
{
    public ValueTask<Result<RecordCandidateResponse>> Handle(RecordCandidateRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(state => Record(request, state)));

    // Phase evaluate too: in mode B the first candidate may be evaluated before the second is recorded.
    private Result<RecordCandidateResponse> Record(RecordCandidateRequest request, BugFixState state)
    {
        var commit = state.Require(Phase.Fix, Phase.Evaluate)
            .Bind(_ => Room(state))
            .Bind(_ => OnBranch(request))
            .Bind(_ => git.Head(request.WorkingDirectory))
            .Bind(head => OnBaseline(request.WorkingDirectory, Base(state), head))
            .Bind(head => New(state, head))
            .Bind(head => Pursues(state, request.Hypothesis).Map(_ => head));
        var changed = commit.Bind(head => git.ChangedFiles(request.WorkingDirectory, Base(state), head))
            .Bind(files => Untouched(files, state.Check?.Path ?? string.Empty, request.AllowTestChanges));
        if (changed.IsError)
        {
            return changed.Error;
        }

        var candidate = new Candidate(
            CandidateId.Numbered(state.Candidates.Count + 1), state.Attempt, state.Mode ?? FixMode.A, request.Branch,
            request.WorkingDirectory, commit.Value, request.Hypothesis, changed.Value, Run: null, Verdict: null);
        store.Save(
            request.StateDirectory,
            (state with { Candidates = [.. state.Candidates, candidate] }).Step(state.Phase, $"record-candidate {candidate.Id.Value}", clock.GetUtcNow()));
        return new RecordCandidateResponse(candidate.Id, candidate.Commit, candidate.ChangedFiles);
    }

    private static string Base(BugFixState state) => state.Baseline?.Commit ?? string.Empty;

    private static Result<Unit> Room(BugFixState state)
    {
        var cap = state.Mode == FixMode.B ? 2 : 1;
        return !state.CurrentCandidates.Skip(cap - 1).Any()
            ? Results.Ok()
            : BugFixErrors.CandidateRefused.With(string.Create(CultureInfo.InvariantCulture, $"mode {Names.Of(state.Mode ?? FixMode.A)} allows {cap} candidate(s) per attempt"));
    }

    private Result<Unit> OnBranch(RecordCandidateRequest request)
    {
        var branch = git.Branch(request.WorkingDirectory);
        if (branch.IsError || branch.Value != request.Branch)
        {
            return branch.IsError ? branch.Error : BugFixErrors.CandidateRefused.With($"{request.WorkingDirectory} is on branch {branch.Value}, not {request.Branch}");
        }

        return git.IsClean(request.WorkingDirectory).Bind<Unit>(clean => clean
            ? Unit.Value
            : BugFixErrors.DirtyTree.With($"{request.WorkingDirectory} has uncommitted or untracked changes; commit the fix first"));
    }

    private Result<string> OnBaseline(string directory, string baseline, string head)
    {
        if (head == baseline)
        {
            return BugFixErrors.CandidateRefused.With("no commits since the baseline; nothing to evaluate");
        }

        var builds = git.IsAncestor(directory, baseline, head);
        if (builds.IsError || !builds.Value)
        {
            return builds.IsError ? builds.Error : BugFixErrors.CandidateRefused.With($"{head} does not build on the baseline {baseline}; start the worktree from the baseline commit");
        }

        return git.SameTree(directory, head, baseline).Bind<string>(same => same
            ? BugFixErrors.CandidateRefused.With("the candidate's tree is identical to the baseline's; nothing was fixed")
            : head);
    }

    private static Result<string> New(BugFixState state, string head) =>
        state.Candidates.FirstOrDefault(c => c.Commit == head) is { } duplicate
            ? BugFixErrors.CandidateRefused.With($"commit {head} is already candidate {duplicate.Id.Value}")
            : head;

    private static Result<Unit> Pursues(BugFixState state, int? number)
    {
        if (number is not { } n)
        {
            return state.Mode == FixMode.B ? BugFixErrors.CandidateRefused.With("mode B candidates must name their hypothesis (--hypothesis <n>)") : Results.Ok();
        }

        if (state.Mode == FixMode.B && state.CurrentCandidates.FirstOrDefault(c => c.Hypothesis == n) is { } taken)
        {
            return BugFixErrors.CandidateRefused.With(string.Create(CultureInfo.InvariantCulture, $"hypothesis {n} is already pursued by {taken.Id.Value} in this attempt"));
        }

        return state.Investigation?.Hypotheses.FirstOrDefault(h => h.Number == n) switch
        {
            null => BugFixErrors.CandidateRefused.With(string.Create(CultureInfo.InvariantCulture, $"the handoff has no hypothesis {n}")),
            { Result: HypothesisResult.RuledOut } => BugFixErrors.CandidateRefused.With(string.Create(CultureInfo.InvariantCulture, $"hypothesis {n} was ruled out")),
            _ => Results.Ok(),
        };
    }

    // Weakening what the check exercises (fixtures, helpers, config) is as bad as editing the check itself.
    private static Result<IReadOnlyList<string>> Untouched(IReadOnlyList<string> files, string checkPath, bool approved)
    {
        var touched = ProtectedPaths.Touched(files, checkPath);
        return touched.Count == 0 || approved
            ? Results.Success(files)
            : BugFixErrors.CandidateRefused.With($"the fix touches test/fixture/config files ({string.Join(", ", touched)}); ask the user, and pass --allow-test-changes only if they approve");
    }
}
