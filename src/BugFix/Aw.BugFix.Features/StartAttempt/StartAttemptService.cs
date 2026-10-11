using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.StartAttempt;

internal sealed class StartAttemptService(IStateStore store, TimeProvider clock) : IService<StartAttemptRequest, StartAttemptResponse>
{
    public ValueTask<Result<StartAttemptResponse>> Handle(StartAttemptRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(state => Start(request, state)));

    private Result<StartAttemptResponse> Start(StartAttemptRequest request, BugFixState state)
    {
        var allowed = state.Require(Phase.Reproduce, Phase.Evaluate).Bind(_ => Allowed(state, request.Mode));
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        var attempt = state.Attempt + 1;
        var mode = Names.Of(request.Mode);
        store.Save(
            request.StateDirectory,
            (state with { Attempt = attempt, Mode = request.Mode, Status = BugFixStatus.Active }).Step(Phase.Fix, $"start-attempt {mode}", clock.GetUtcNow()));
        return new StartAttemptResponse(Phase.Fix, attempt, request.Mode);
    }

    private static Result<Unit> Allowed(BugFixState state, FixMode mode)
    {
        var current = state.CurrentCandidates.ToList();
        if (current.Find(static c => c.Resolves) is { } resolving)
        {
            return BugFixErrors.AttemptRefused.With($"candidate {resolving.Id.Value} already resolves the ticket; run advance report --candidate {resolving.Id.Value}");
        }

        if (current.Find(static c => !c.Evaluated) is { } pending)
        {
            return BugFixErrors.AttemptRefused.With($"candidate {pending.Id.Value} has not been evaluated yet");
        }

        if (state.Attempt >= BugFixState.MaxAttempts)
        {
            return BugFixErrors.AttemptRefused.With($"attempt cap ({BugFixState.MaxAttempts}) reached; run advance report --unresolved");
        }

        return mode == FixMode.B ? Competing(state) : Results.Ok();
    }

    // Mode B (competing implementers) is the escalation after a failed attempt, one implementer per open hypothesis.
    private static Result<Unit> Competing(BugFixState state)
    {
        if (state.Attempt == 0)
        {
            return BugFixErrors.AttemptRefused.With("mode B (competing implementers) is the escalation after a failed attempt");
        }

        var open = state.Investigation?.Hypotheses.Count(static h => h.Result != HypothesisResult.RuledOut) ?? 0;
        return open >= 2
            ? Results.Ok()
            : BugFixErrors.AttemptRefused.With(string.Create(CultureInfo.InvariantCulture, $"mode B needs at least 2 hypotheses not ruled out; the handoff has {open}"));
    }
}
