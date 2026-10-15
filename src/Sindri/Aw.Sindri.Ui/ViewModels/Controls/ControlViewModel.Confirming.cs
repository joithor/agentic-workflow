namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>A destructive control's in-app confirm (<see cref="ControlViewModel"/>, continued; §8, E-8).</summary>
internal sealed partial class ControlViewModel
{
    /// <summary>True when the catalog marks the action destructive (E-8): a press asks in-app before it sends (§8).</summary>
    public bool IsDestructive { get; }

    /// <summary>True while a destructive control asks "… ?" with Confirm and Keep.</summary>
    public bool IsConfirming { get; private set => Set(ref field, value); }

    /// <summary>The confirm row's Confirm label.</summary>
    public string ConfirmLabel { get; } = Labels.Confirm;

    /// <summary>The confirm row's Keep label.</summary>
    public string KeepLabel { get; } = Labels.Keep;

    /// <summary>The question a destructive control asks: its name and a question mark ("Close as cancelled ITM-…?").</summary>
    public string ConfirmQuestion { get; }

    /// <summary>The Confirm button's accessible name: "Confirm: Close as cancelled ITM-…?".</summary>
    public string ConfirmName => ConfirmLabel;

    /// <summary>The Keep button's accessible name: "Keep ITM-…".</summary>
    public string KeepName => KeepLabel;

    /// <summary>A press: a destructive control asks first; any other sends at once.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown, or at once when it asks.</returns>
    internal Task Press(CancellationToken cancellationToken)
    {
        if (!IsDestructive || !CanRun)
        {
            return Run(cancellationToken);
        }

        IsConfirming = true;
        return Task.CompletedTask;
    }

    /// <summary>The asked question's Confirm: sends once.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Confirm(CancellationToken cancellationToken)
    {
        if (!IsConfirming)
        {
            return Task.CompletedTask;
        }

        IsConfirming = false;
        return Run(cancellationToken);
    }

    /// <summary>The asked question's Keep (or Esc): nothing is sent.</summary>
    internal void Keep() => IsConfirming = false;
}
