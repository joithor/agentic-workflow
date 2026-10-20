using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Focus control of a session row (collector spec CU-2, partial): the catalog row FocusSession is pending until CO-4a, so it draws
/// a disabled button with its phase and the CLI to run, filled with the row's ref, on a focusable line with Copy. A ready row with no sender
/// still draws this way until U11a-f wires the send, so the app never shows a dead button.
/// </summary>
internal sealed class FocusLine : ObservableObject
{
    private readonly string _template;
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;
    private int _copies;

    internal FocusLine(string reference, CommandRowView? row, IClipboard clipboard, TimeProvider clock)
    {
        (_clipboard, _clock) = (clipboard, clock);
        _template = row is { Cli.Length: > 0 } ? row.Cli : Labels.FocusCliFallback;
        Show(reference);
    }

    /// <summary>The control's label.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Label => Labels.FocusLabel;
#pragma warning restore CA1822, MA0041

    /// <summary>The button's accessible name: "Watch the agent, coming soon".</summary>
    public string Name { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The CLI to run, with the row's ref in it; empty when the ref holds a control character.</summary>
    public string Cli { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The line: "Coming soon · sindri agents focus claude-code/…".</summary>
    public string Line { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Copy", or "Copied" for a moment.</summary>
    public string CopyLabel { get; private set { Set(ref field, value); OnChanged(nameof(CopyName)); } } = Labels.Copy;

    /// <summary>The Copy button's accessible name.</summary>
    public string CopyName => string.Equals(CopyLabel, Labels.Copied, StringComparison.Ordinal) ? Labels.Copied : Labels.Copy + ": " + Labels.FocusLabel + " " + Cli;

    /// <summary>True when the CLI can be copied.</summary>
    public bool CanCopy => Cli.Length > 0;

    /// <summary>Fills the CLI for a ref.</summary>
    /// <param name="reference">The session's ref.</param>
    internal void Show(string reference)
    {
        Cli = reference.Any(char.IsControl) ? string.Empty : _template.Replace("<ref>", reference, StringComparison.Ordinal);
        Line = Labels.FocusSoon + " · " + Cli;
        Name = Labels.FocusName;
        OnChanged(nameof(CanCopy));
        OnChanged(nameof(CopyName));
    }

    /// <summary>Copies the CLI: "Copied" for 1.5 s.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends once the clipboard answered.</returns>
    internal async Task Copy(CancellationToken cancellationToken)
    {
        if (!CanCopy || !await _clipboard.SetText(Cli, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyLabel = Labels.Copied;
        _ = Uncopy(++_copies, cancellationToken);
    }

    // "Copied" shows for a moment, then the label rests; the copy itself has already finished.
    private async Task Uncopy(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ControlViewModel.CopiedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (copy == _copies)
        {
            CopyLabel = Labels.Copy;
        }
    }
}
