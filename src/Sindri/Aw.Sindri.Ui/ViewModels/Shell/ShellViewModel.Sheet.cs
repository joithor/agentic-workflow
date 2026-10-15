using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The detail sheet and its subject (UI spec §3.3, §4.2): open, closed to the edge tab, reopened. <see cref="Navigate"/> opens it.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>True while the detail sheet is open.</summary>
    public bool IsSheetOpen { get; private set; }

    /// <summary>The sheet's subject (the deep link's subject); kept after the sheet closes for the edge tab; empty before the first.</summary>
    public string SheetSubject => SheetLink?.Subject ?? string.Empty;

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

    /// <summary>True while the sheet shows its "not served yet" placeholder: until a lane's content claims the link's kind.</summary>
#pragma warning disable IDE0072 // the lanes populate the kinds they claim; every other kind keeps the placeholder
    public bool IsSheetPlaceholderShown => SheetLink?.Kind switch
    {
        // LANE ANCHOR U3': one `DeepLinkKind.X or … => false` arm for the Needs you kinds claims them from the placeholder. Insert immediately above this marker, never between the markers.
        // LANE ANCHOR U8: one `DeepLinkKind.X or … => false` arm for the instance kinds. Insert immediately below this marker, never between the markers.
        _ => true,
    };
#pragma warning restore IDE0072

    /// <summary>Opens the sheet on a deep link's subject, keeping the link's kind (an item and a workflow with one subject are two sheets).</summary>
    /// <param name="link">The link, with a subject.</param>
    internal void OpenSheet(DeepLink link)
    {
        SheetLink = link;
        IsSheetOpen = true;
        Changed(nameof(SheetLink), nameof(SheetSubject), nameof(EdgeTabName), nameof(IsSheetOpen), nameof(HasEdgeTab), nameof(IsSheetShown), nameof(IsEdgeTabShown), nameof(IsSheetPlaceholderShown));
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
}
