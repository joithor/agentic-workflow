using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Deep links (UI spec §3.3, screens spec §3): where a link goes.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Goes where a deep link points: a page, or the detail sheet with its subject.</summary>
    /// <param name="link">The link.</param>
    internal void Navigate(DeepLink link)
    {
        if (link.Kind == DeepLinkKind.HomeNeedsYou || link.Subject is null)
        {
            GoTo(PageId.Home);
            return;
        }

        if (PageOf(link.Kind) is { } page)
        {
            GoTo(page);
        }

        SheetSubject = link.Subject;
        IsSheetOpen = true;
        Changed(nameof(SheetSubject), nameof(EdgeTabName), nameof(IsSheetOpen), nameof(HasEdgeTab), nameof(IsSheetShown), nameof(IsEdgeTabShown));
    }

    // The rev 8 links name a page (screens spec §3); the older ones open their sheet over the page shown.
    private static PageId? PageOf(DeepLinkKind kind) => kind switch
    {
        DeepLinkKind.Workflow => PageId.Workflows,
        DeepLinkKind.Recipe => PageId.Recipes,
        DeepLinkKind.Node => PageId.Context,
        DeepLinkKind.Session => PageId.Agents,
        DeepLinkKind.System => PageId.System,
        DeepLinkKind.HomeNeedsYou or DeepLinkKind.NeedsYou or DeepLinkKind.Item or DeepLinkKind.Decision or DeepLinkKind.Service or DeepLinkKind.Job => null,
        _ => null,
    };
}
