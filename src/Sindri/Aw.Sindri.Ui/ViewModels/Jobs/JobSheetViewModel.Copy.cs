using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>The job sheet's Copy button (<see cref="JobSheetViewModel"/>, continued): the cancel's terminal form to the clipboard, "Copied" for a moment.</summary>
internal sealed partial class JobSheetViewModel
{
    /// <summary>"Copy", or "Copied" for a moment.</summary>
    public string CopyLabel
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CopyName));
        }
    } = Labels.Copy;

    /// <summary>The Copy button's accessible name: "Copy: To cancel from a terminal", then "Copied".</summary>
    public string CopyName => string.Equals(CopyLabel, Labels.Copied, StringComparison.Ordinal) ? Labels.Copied : Labels.Copy + ": " + Labels.JobCliField;

    /// <summary>Copies the cancel's terminal form: "Copied" for <see cref="ControlViewModel.CopiedFor"/>.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends when the clipboard answered.</returns>
    internal async Task CopyCli(CancellationToken cancellationToken)
    {
        if (_catalog is not { } catalog || Cli.Length == 0 || !await catalog.Clipboard.SetText(Cli, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyLabel = Labels.Copied;
        _ = Uncopy(++_copies, cancellationToken);
    }

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

    /// <summary>The job id's copy chip: "Copy", or "Copied" for a moment.</summary>
    public string CopyIdLabel
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CopyIdName));
        }
    } = Labels.Copy;

    /// <summary>The chip's accessible name: "Copy: Job id", then "Copied".</summary>
    public string CopyIdName => string.Equals(CopyIdLabel, Labels.Copied, StringComparison.Ordinal) ? Labels.Copied : Labels.Copy + ": " + Labels.JobIdField;

    /// <summary>Copies the job's id: "Copied" for <see cref="ControlViewModel.CopiedFor"/>.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends when the clipboard answered.</returns>
    internal async Task CopyJobId(CancellationToken cancellationToken)
    {
        if (_catalog is not { } catalog || Id.Length == 0 || !await catalog.Clipboard.SetText(Id, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyIdLabel = Labels.Copied;
        _ = UncopyId(++_idCopies, cancellationToken);
    }

    private async Task UncopyId(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ControlViewModel.CopiedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (copy == _idCopies)
        {
            CopyIdLabel = Labels.Copy;
        }
    }
}
