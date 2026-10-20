using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Deep links (UI spec §3.3, screens spec §3): where a link goes.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Goes where a deep link points: a page, or the detail sheet with its subject.</summary>
    /// <param name="link">The link.</param>
    internal void Navigate(DeepLink link)
    {
        if (link.Kind == DeepLinkKind.Setup)
        {
            GoTo(PageId.Settings);   // the checklist is Settings' first section; ShowPage scrolls to it
            return;
        }

        if (link.Kind == DeepLinkKind.HomeNeedsYou || link.Subject is null)
        {
            GoTo(PageId.Home);
            return;
        }

        link = Canonical(link);
        if (PageOf(link.Kind) is { } page)
        {
            GoTo(page);
        }

        OpenSheet(link);
        if (link.Kind is DeepLinkKind.NeedsYou or DeepLinkKind.Decision)
        {
            SelectRowOf(link);
        }
    }

    // One rule for a row's link (the tray, a deep link, a click): a needs-you link whose row is a decision is the decision's own link.
    private DeepLink Canonical(DeepLink link) =>
        link.Kind == DeepLinkKind.NeedsYou && link.Subject is { } rowId
        && NeedsYou.Row(rowId) is { Entry.DecisionId: { } decisionId }
            ? new DeepLink(DeepLinkKind.Decision, decisionId)
            : link;

    // The rev 8 links name a page (screens spec §3); the older ones open their sheet over the page shown.
    private static PageId? PageOf(DeepLinkKind kind) => kind switch
    {
        DeepLinkKind.Workflow => PageId.Workflows,
        DeepLinkKind.Recipe => PageId.Recipes,
        DeepLinkKind.Node => PageId.Context,
        DeepLinkKind.Session => PageId.Agents,
        DeepLinkKind.System => PageId.System,
        DeepLinkKind.HomeNeedsYou or DeepLinkKind.NeedsYou or DeepLinkKind.Item or DeepLinkKind.Decision or DeepLinkKind.Service or DeepLinkKind.Job or DeepLinkKind.Setup => null,
        _ => null,
    };
}
