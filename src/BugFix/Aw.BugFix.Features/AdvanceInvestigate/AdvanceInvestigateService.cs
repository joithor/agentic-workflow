using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.AdvanceInvestigate;

internal sealed class AdvanceInvestigateService(IStateStore store, IFiles files, TimeProvider clock)
    : IService<AdvanceInvestigateRequest, AdvanceInvestigateResponse>
{
    public ValueTask<Result<AdvanceInvestigateResponse>> Handle(AdvanceInvestigateRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(state => Advance(request, state)));

    private Result<AdvanceInvestigateResponse> Advance(AdvanceInvestigateRequest request, BugFixState state)
    {
        var handoff = state.Require(Phase.Intake)
            .Bind(_ => files.ReadText(request.Handoff))
            .Bind(HandoffParser.Parse)
            .Bind(Supports);
        if (handoff.IsError)
        {
            return handoff.Error;
        }

        var investigation = new Investigation(request.Handoff, handoff.Value.RootCause, handoff.Value.Hypotheses);
        store.Save(
            request.StateDirectory,
            (state with { Investigation = investigation }).Step(Phase.Investigate, "advance investigate", clock.GetUtcNow(), request.Handoff));
        return new AdvanceInvestigateResponse(Phase.Investigate, handoff.Value.Hypotheses.Count);
    }

    // Never fix an unconfirmed cause.
    private static Result<Handoff> Supports(Handoff handoff)
    {
        if (handoff.Status != "diagnosed")
        {
            return BugFixErrors.HandoffRefused.With($"handoff status is \"{handoff.Status}\", expected \"diagnosed\" (run /rootCause --investigate-only)");
        }

        if (!handoff.Hypotheses.Any(static h => h.Result == HypothesisResult.Confirmed))
        {
            return BugFixErrors.HandoffRefused.With("no hypothesis is confirmed; ask the user before fixing an unconfirmed cause");
        }

        return handoff.RootCause.Length == 0 ? BugFixErrors.HandoffRefused.With("the handoff has no ## Root Cause text") : handoff;
    }
}
