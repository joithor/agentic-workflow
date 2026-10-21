using Aw.Sindri.Ui.Platform;

#pragma warning disable CA1822, MA0041, MA0182, IDE0021 // the red stub: every member is a default until the green commit
namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The action bar under the inbox's request card (F3 needs-you spec §3, §5): stub, red first.</summary>
internal sealed class NeedsYouBarViewModel : ObservableObject
{
    internal NeedsYouBarViewModel(DeskReporter desk, IClipboard clipboard, TimeProvider clock)
    {
        _ = (desk, clipboard, clock);
    }

    /// <summary>Raised when the bar asks to answer a decision.</summary>
    internal event EventHandler<NeedsYouDecisionEventArgs>? DecisionRequested;

    internal NeedsYouBarMode Mode { get; private set; }

    internal bool ReadToEnd { get; set; }

    internal string ApproveWord => "";

    internal string SendBackWord => "";

    internal bool ShowsPrimary => false;

    internal bool ShowsTerminal => false;

    internal string TerminalWord => "";

    internal bool ShowsSendBack => false;

    internal bool SendBackEnabled => false;

    internal string SendBackTooltip => "";

    internal bool ShowsEditChip => false;

    internal bool ShowsDrop => false;

    internal bool ShowsRowControls => false;

    internal string Hint => "";

    internal string ReasonText { get; set; } = "";

    internal bool CanSend => false;

    internal bool IsApproved => false;

    internal string Refusal => "";

    internal void Show(NeedsYouRowViewModel? row) => _ = (row, DecisionRequested);

    internal Task PressApprove() => Task.CompletedTask;

    internal Task PressSendBack() => Task.CompletedTask;

    internal Task Send() => Task.CompletedTask;

    internal void Cancel() => Mode = NeedsYouBarMode.Pending;

    internal Task RequestDrop() => Task.CompletedTask;

    internal Task ConfirmDrop() => Task.CompletedTask;

    internal Task CopyTerminal() => Task.CompletedTask;

    /// <summary>Completes when the desk run of the shown row has been read.</summary>
    internal Task Loaded { get; } = Task.CompletedTask;
}
