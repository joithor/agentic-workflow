using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The session sheet in the shell (<see cref="ShellViewModel"/>, continued; U11a T3, T4): a session link or a row's request opens it, it re-reads while open, and Esc knows the row.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The session whose Agents row opened the sheet (its ref), where Esc returns focus; null when it was opened some other way.</summary>
    internal string? OpenerSession { get; private set; }

    partial void InitAgentSheet()
    {
        AgentSheet.Wire(Health.Catalog, _clock);
        Health.Follow((url, token) => IsSessionSheet && IsSheetOpen ? AgentSheet.Refresh(url, token) : Task.CompletedTask);
        Agents.OpenRequested += (_, e) =>
        {
            OpenSheet(e.Link);
            OpenerSession = e.Link.Subject;   // Esc comes back to this row, after the sheet's own opening cleared it
        };
    }

    partial void OnAgentSheetOpened(DeepLink link)
    {
        OpenerSession = null;
        Changed(nameof(IsSessionSheet));
        if (link.Kind != DeepLinkKind.Session || link.Subject is not { } subject)
        {
            return;
        }

        AgentSheet.Show(subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = AgentSheet.Open(url, subject, CancellationToken.None);
        }
    }
}
