#pragma warning disable CA1822, MA0041, IDE0021, MA0003, CS0067, CS9113, IDE0060, CA1801 // red stub: the members throw until the green commit
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One command the engine lists for the state.</summary>
internal sealed class ExpandedCommandViewModel
{
    internal ExpandedCommandViewModel(StatusCommand command, AnswerKind? answer, string address, IClipboard clipboard, TimeProvider clock) => throw new NotSupportedException();

    /// <summary>The verb.</summary>
    public StatusVerb Verb => throw new NotSupportedException();

    /// <summary>The words.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>The accessible name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>The served CLI line, or empty when it can't be shown.</summary>
    public string Cli => throw new NotSupportedException();

    /// <summary>The served confirm sentence, or null.</summary>
    public string? Confirm => throw new NotSupportedException();

    /// <summary>The chip that stands for the send, which the app does not make yet.</summary>
    public string PendingChip => throw new NotSupportedException();

    /// <summary>The lock button for a protected command; null for the others.</summary>
    public ProtectedActionViewModel? Protected => throw new NotSupportedException();

    /// <summary>The Copy button's words.</summary>
    public string CopyLabel => throw new NotSupportedException();

    /// <summary>The Copy button's accessible name.</summary>
    public string CopyName => throw new NotSupportedException();

    /// <summary>Copies the CLI.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task Copy(CancellationToken cancellationToken) => throw new NotSupportedException();
}
