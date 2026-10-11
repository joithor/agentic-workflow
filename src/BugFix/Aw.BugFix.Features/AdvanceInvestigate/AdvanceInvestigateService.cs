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
        ValueTask.FromResult(store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Bind(state => Advance(request, state))));

    // From here on the helper uses its own snapshot of the handoff; editing the file changes nothing.
    private Result<AdvanceInvestigateResponse> Advance(AdvanceInvestigateRequest request, BugFixState state)
    {
        var handoff = state.Require(Phase.Intake)
            .Bind(_ => files.ReadText(request.Handoff))
            .Bind(HandoffParser.Parse)
            .Bind(static h => h.SupportsFix());
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
}
