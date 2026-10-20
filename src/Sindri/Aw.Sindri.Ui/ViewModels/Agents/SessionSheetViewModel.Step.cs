using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The session sheet's Step (<see cref="SessionSheetViewModel"/>, continued): the instance, state and attempt an engine launch started the session for, and the action that opens that frame.</summary>
internal sealed partial class SessionSheetViewModel
{
    private SessionLaunchView? _step;

    /// <summary>Raised when the sheet asks to open the frame of the Step the session works on.</summary>
    internal event EventHandler<SessionFrameRequestedEventArgs>? FrameRequested;

    /// <summary>True when an engine launch started the session (the open action shows).</summary>
    public bool HasStep => _step is { Instance.Length: > 0 };

    /// <summary>The open action's visible words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string OpenFrameLabel => Labels.OpenStepFrame;
#pragma warning restore CA1822, MA0041

    /// <summary>The open action's accessible name: "Open ITM-…/step/1 at build".</summary>
    public string OpenFrameName => _step is { } step && step.Instance is { Length: > 0 } ? Labels.OpenStepFrameName(step.State) : Labels.OpenStepFrame;

    /// <summary>Opens the frame of the Step the session works on; nothing without one.</summary>
    internal void OpenFrame()
    {
        if (_step is { Instance: { Length: > 0 } instance } step)
        {
            FrameRequested?.Invoke(this, new SessionFrameRequestedEventArgs(instance, step.State));
        }
    }

    private (string Label, string? Value)[] StepFields(SessionRowView session)
    {
        _step = session.Launch;
        OnChanged(nameof(HasStep));
        OnChanged(nameof(OpenFrameName));
        return
        [
            (Labels.SheetInstance, session.Launch?.Instance),
            (Labels.SheetStep, session.Launch?.State),
            (Labels.SheetAttempt, session.Launch?.Attempt is { } attempt ? Number(attempt) : null),
        ];
    }
}
