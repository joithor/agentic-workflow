using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.StartAttempt;

internal sealed class StartAttemptService(IStateStore store, TimeProvider clock) : IService<StartAttemptRequest, StartAttemptResponse>
{
    public ValueTask<Result<StartAttemptResponse>> Handle(StartAttemptRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Bind(state => Start(request, state))));

    private Result<StartAttemptResponse> Start(StartAttemptRequest request, BugFixState state)
    {
        var allowed = state.Require(Phase.Reproduce, Phase.Evaluate).Bind(_ => AttemptRule.CanStart(state, request.Mode, request.AbandonJudge));
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        var attempt = state.Attempt + 1;
        store.Save(
            request.StateDirectory,
            (state with { Attempt = attempt, Mode = request.Mode, Status = BugFixStatus.Active })
                .Step(Phase.Fix, $"start-attempt {Names.Of(request.Mode)}{(request.AbandonJudge ? " --abandon-judge" : string.Empty)}", clock.GetUtcNow()));
        return new StartAttemptResponse(Phase.Fix, attempt, request.Mode);
    }
}
