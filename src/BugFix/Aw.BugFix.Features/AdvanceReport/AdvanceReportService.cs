using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.AdvanceReport;

internal sealed class AdvanceReportService(IStateStore store, TimeProvider clock) : IService<AdvanceReportRequest, AdvanceReportResponse>
{
    public ValueTask<Result<AdvanceReportResponse>> Handle(AdvanceReportRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult((request.Candidate is null) == request.Unresolved
            ? store.Load(request.StateDirectory).Bind(state => Report(request, state))
            : Results.Failure<AdvanceReportResponse>(BugFixErrors.Invalid.With("pass exactly one of --candidate <id> or --unresolved")));

    private Result<AdvanceReportResponse> Report(AdvanceReportRequest request, BugFixState state)
    {
        if (state.Phase == Phase.Report)
        {
            return BugFixErrors.ReportRefused.With("already reported");
        }

        if (request.Candidate is not { } id)
        {
            store.Save(request.StateDirectory, (state with { Status = BugFixStatus.Unresolved }).Step(Phase.Report, "advance report --unresolved", clock.GetUtcNow()));
            return new AdvanceReportResponse(Phase.Report, BugFixStatus.Unresolved, Candidate: null);
        }

        var candidate = state.Find(id).Bind(Resolving);
        if (candidate.IsError)
        {
            return candidate.Error;
        }

        store.Save(
            request.StateDirectory,
            (state with { Status = BugFixStatus.Resolved, ResolvedBy = id }).Step(Phase.Report, $"advance report {id.Value}", clock.GetUtcNow(), candidate.Value.Run?.Evidence));
        return new AdvanceReportResponse(Phase.Report, BugFixStatus.Resolved, id);
    }

    // "Resolved" is recorded only with a passing run and a resolved judge decision.
    private static Result<Candidate> Resolving(Candidate candidate)
    {
        if (candidate.Resolves)
        {
            return candidate;
        }

        var run = candidate.Run switch { null => "none", { Passed: true } => "passed", _ => "failed" };
        var verdict = candidate.Verdict is null ? "none" : candidate.Verdict.Decision.ToString();
        return BugFixErrors.ReportRefused.With($"{candidate.Id.Value} needs a passing run and a \"resolved\" judge decision (run: {run}, judge: {verdict})");
    }
}
