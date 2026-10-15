using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The detail sheet and its subject (UI spec §3.3, §4.2): open, closed to the edge tab, reopened. <see cref="Navigate"/> opens it.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>True while the detail sheet is open.</summary>
    public bool IsSheetOpen { get; private set; }

    /// <summary>The sheet's subject (the deep link's subject); kept after the sheet closes for the edge tab; empty before the first.</summary>
    public string SheetSubject { get; private set; } = string.Empty;

    /// <summary>The link the sheet was opened by (its kind and subject); null before the first.</summary>
    public DeepLink? SheetLink { get; private set; }

    /// <summary>True when the closed sheet shows its edge tab (a subject was opened before).</summary>
    public bool HasEdgeTab => SheetSubject.Length > 0;

    /// <summary>The sheet shows when it's open over a visible page.</summary>
    public bool IsSheetShown => IsSheetOpen && IsConnected;

    /// <summary>The edge tab's accessible name: "Reopen" and its subject.</summary>
    public string EdgeTabName => Labels.Reopen + " " + SheetSubject;

    /// <summary>The edge tab shows when the sheet is closed and has a subject, over a visible page.</summary>
    public bool IsEdgeTabShown => !IsSheetOpen && HasEdgeTab && IsConnected;

    /// <summary>Opens the sheet on a subject (a deep link's).</summary>
    /// <param name="subject">The subject, as the link carries it.</param>
    internal void OpenSheet(string subject)
    {
        SheetSubject = subject;
        IsSheetOpen = true;
        Changed(nameof(SheetSubject), nameof(EdgeTabName), nameof(IsSheetOpen), nameof(HasEdgeTab), nameof(IsSheetShown), nameof(IsEdgeTabShown));
    }

    /// <summary>Closes the sheet (Esc); its subject stays on the edge tab.</summary>
    internal void CloseSheet()
    {
        IsSheetOpen = false;
        Changed(nameof(IsSheetOpen), nameof(IsSheetShown), nameof(IsEdgeTabShown));
    }

    /// <summary>Opens the sheet again on its last subject (the edge tab).</summary>
    internal void ReopenSheet()
    {
        if (!HasEdgeTab)
        {
            return;
        }

        IsSheetOpen = true;
        Changed(nameof(IsSheetOpen), nameof(IsSheetShown), nameof(IsEdgeTabShown));
    }

    // LANE ANCHOR U3': the item sheet's subject kinds are routed from OpenSheet; add U3' kinds here.
    // LANE ANCHOR U8: the instance sheet's tabs and subject kinds are routed from OpenSheet; add U8 kinds here.
}
