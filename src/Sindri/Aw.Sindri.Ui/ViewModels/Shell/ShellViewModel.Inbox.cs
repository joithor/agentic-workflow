using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Needs-you inbox in the shell (<see cref="ShellViewModel"/>, continued): the page that opens over Home's panel.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The inbox page.</summary>
    public NeedsYouPageViewModel Inbox { get; private set; } = null!;

    /// <summary>True while the inbox shows in place of Home's panels (Home stays the page).</summary>
    public bool IsNeedsYouInbox => false;

    /// <summary>Opens the inbox, on a row when one is named.</summary>
    /// <param name="rowId">The row to select; null for the first.</param>
    internal void OpenNeedsYouInbox(string? rowId = null) => throw new NotSupportedException();

    /// <summary>Leaves the inbox for Home (Esc, the breadcrumb).</summary>
    /// <returns>True when the inbox was open.</returns>
    internal bool LeaveNeedsYouInbox() => throw new NotSupportedException();
}
