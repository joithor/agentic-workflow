using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.JudgeCandidate;

/// <summary>
/// Asks judge resolution-check exactly once per candidate, on an input built from state (the ticket verbatim, the
/// snapshotted root cause, a description of the frozen check, the diff), and records that verdict. The orchestrator never
/// picks among verdicts, so one can't be re-rolled.
/// </summary>
internal sealed class JudgeCandidateService(IStateStore store, IRunLedger ledger, IGit git, IJudge judge, TimeProvider clock)
    : IService<JudgeCandidateRequest, JudgeCandidateResponse>
{
    public ValueTask<Result<JudgeCandidateResponse>> Handle(JudgeCandidateRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(state => Judge(request, state)));

    private Result<JudgeCandidateResponse> Judge(JudgeCandidateRequest request, BugFixState state)
    {
        var candidate = state.Require(Phase.Evaluate).Bind(_ => state.Find(request.Candidate)).Bind(Judgeable);
        var input = candidate.Bind(c => Input(state, c));
        if (input.IsError)
        {
            return input.Error;
        }

        var inputSha = ledger.WriteJudgeInput(request.StateDirectory, request.Candidate, input.Value);
        var answer = judge.ResolutionCheck(input.Value);
        return answer.Bind(a => Record(request, a, inputSha));
    }

    private static Result<Candidate> Judgeable(Candidate candidate)
    {
        if (candidate.Run is not { Passed: true })
        {
            return BugFixErrors.JudgeRefused.With($"{candidate.Id.Value} has no passing run; judge only runs after the check passes");
        }

        return candidate.Verdict is null
            ? candidate
            : BugFixErrors.JudgeRefused.With($"{candidate.Id.Value} was already judged ({candidate.Verdict.Decision}); a verdict is never re-asked");
    }

    // Built from state, never from agent-written text, so it can't steer the verdict.
    private Result<ResolutionCheckInput> Input(BugFixState state, Candidate candidate)
    {
        var check = state.Check!; // why: phase evaluate comes after advance reproduce froze the check.
        var summary = check.Kind == CheckKind.Test
            ? $"the regression test {check.Path}, run as `{string.Join(' ', check.Command ?? [])}`"
            : $"the ui-evidence script {check.Path}";
        return git.Diff(candidate.Worktree, state.Baseline!.Commit, candidate.Commit).Map(diff => new ResolutionCheckInput( // why: as above, the baseline is set with the check.
            state.Ticket.Brief, state.Ticket.Expected, state.Ticket.Actual, state.Investigation?.RootCause ?? string.Empty, check.Kind, summary,
            BeforePassed: false, AfterPassed: true, diff.Stat, Capped(diff.Full)));
    }

    private static string? Capped(string? diff) =>
        diff is null || diff.Length <= ResolutionCheckInput.DiffCap
            ? diff
            : string.Create(CultureInfo.InvariantCulture, $"{diff[..ResolutionCheckInput.DiffCap]}\n[truncated {diff.Length - ResolutionCheckInput.DiffCap} chars]");

    // The judge call can take minutes: record onto the current state, and never over a verdict another call recorded meanwhile.
    private Result<JudgeCandidateResponse> Record(JudgeCandidateRequest request, JudgeAnswer answer, string inputSha)
    {
        var fresh = store.Load(request.StateDirectory);
        var candidate = fresh.Bind(state => state.Find(request.Candidate));
        if (candidate.IsError)
        {
            return candidate.Error;
        }

        if (candidate.Value.Verdict is not null)
        {
            return BugFixErrors.JudgeRefused.With($"{request.Candidate.Value} was judged by another call meanwhile; that verdict stands");
        }

        var escalated = answer.Decision == JudgeDecision.Escalated;
        var verdict = new JudgeVerdict(answer.Decision, answer.DecisionId, answer.ReasonCode, inputSha);
        var state = fresh.Value.With(candidate.Value with { Verdict = verdict }) with
        {
            Status = escalated ? BugFixStatus.NeedsHuman : fresh.Value.Status,
        };
        store.Save(request.StateDirectory, state.Step(Phase.Evaluate, $"judge {request.Candidate.Value}", clock.GetUtcNow()));
        return new JudgeCandidateResponse(request.Candidate, answer.Decision, answer.DecisionId, answer.ReasonCode, answer.Reasons, state.Status);
    }
}
