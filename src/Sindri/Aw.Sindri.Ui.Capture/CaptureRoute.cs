using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.Capture;

/// <summary>Where a screen is: one of the app's pages, or a deep link, the same places a click or the palette goes to.</summary>
internal abstract record CaptureRoute
{
    /// <summary>Reads a route's text: 'page:Workflows' (a <see cref="PageId"/>), or a deep link ('item/ITM-01JE7', 'workflow/PRJ-sync', 'recipe/triage.alert').</summary>
    /// <param name="text">The route.</param>
    /// <returns>The route, or null when the text is neither.</returns>
    internal static CaptureRoute? Parse(string text)
    {
        if (text.StartsWith("page:", StringComparison.Ordinal))
        {
            return Enum.TryParse<PageId>(text["page:".Length..], ignoreCase: false, out var page) && Enum.IsDefined(page) ? new OnPage(page) : null;
        }

        return DeepLink.TryParse(text, out var link) ? new OnLink(link) : null;
    }

    /// <summary>A page.</summary>
    /// <param name="Page">The page.</param>
    internal sealed record OnPage(PageId Page) : CaptureRoute;

    /// <summary>A deep link.</summary>
    /// <param name="Link">The link.</param>
    internal sealed record OnLink(DeepLink Link) : CaptureRoute;
}
