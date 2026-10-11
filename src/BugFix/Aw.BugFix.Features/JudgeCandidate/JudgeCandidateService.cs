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
    public ValueTask<Result<JudgeCandidateResponse>> Handle(JudgeCandidateRequest request, CancellationToken cancellationToken)
    {
        var question = store.Load(request.StateDirectory)
            .Bind(state => state.Require(Phase.Evaluate).Bind(_ => state.Find(request.Candidate)).Bind(static c => c.Judgeable())
                .Bind(candidate => Input(state, candidate)))
            .Map(input => ledger.WriteJudgeInput(request.StateDirectory, request.Candidate, input));
        var answer = question.Bind(q => judge.ResolutionCheck(q).Bind(a => SameInput(q, a)));

        // The judge call can take minutes: record under the lock onto the current state, never over a verdict another
        // call recorded meanwhile.
        return ValueTask.FromResult(answer.Bind(a => store.Locked(request.StateDirectory, () => Record(request, a))));
    }

    // Built from state, never from agent-written text, so it can't steer the verdict.
    private Result<ResolutionCheckInput> Input(BugFixState state, Candidate candidate)
    {
        var (check, baseline) = state.Frozen;
        var summary = check.Kind == CheckKind.Test
            ? $"the regression test {check.Path}, run as `{string.Join(' ', check.Command ?? [])}`"
            : $"the ui-evidence script {check.Path}";
        return git.Diff(candidate.Worktree, baseline.Commit, candidate.Commit).Map(diff => new ResolutionCheckInput(
            state.Ticket.Brief, state.Ticket.Expected, state.Ticket.Actual, state.Investigation?.RootCause ?? string.Empty, check.Kind, summary,
            BeforePassed: false, AfterPassed: true, diff.Stat, Capped(diff.Full)));
    }

    private static string? Capped(string? diff) =>
        diff is null || diff.Length <= ResolutionCheckInput.DiffCap
            ? diff
            : string.Create(CultureInfo.InvariantCulture, $"{diff[..ResolutionCheckInput.DiffCap]}\n[truncated {diff.Length - ResolutionCheckInput.DiffCap} chars]");

    // A verdict counts only for the input the helper wrote.
    private static Result<JudgeAnswer> SameInput(JudgeQuestion question, JudgeAnswer answer) =>
        answer.InputSha256 == question.InputSha256
            ? answer
            : BugFixErrors.JudgeUnavailable.With($"judge answered about input {answer.InputSha256}, not the input the helper wrote ({question.InputSha256}); no verdict recorded");

    private Result<JudgeCandidateResponse> Record(JudgeCandidateRequest request, JudgeAnswer answer)
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

        var status = answer.Decision switch
        {
            JudgeDecision.Escalated => BugFixStatus.NeedsHuman,
            JudgeDecision.Resolved => BugFixStatus.Active,   // a resolving candidate supersedes an earlier escalation in the attempt
            JudgeDecision.Partial or JudgeDecision.Unresolved => fresh.Value.Status,
            _ => throw new ArgumentOutOfRangeException(nameof(answer), answer.Decision, "not a decision"),
        };
        var verdict = new JudgeVerdict(answer.Decision, answer.DecisionId, answer.ReasonCode, answer.InputSha256);
        var state = fresh.Value.With(candidate.Value with { Verdict = verdict }) with { Status = status };
        store.Save(request.StateDirectory, state.Step(Phase.Evaluate, $"judge {request.Candidate.Value}", clock.GetUtcNow()));
        return new JudgeCandidateResponse(request.Candidate, answer.Decision, answer.DecisionId, answer.ReasonCode, answer.Reasons, status);
    }
}
