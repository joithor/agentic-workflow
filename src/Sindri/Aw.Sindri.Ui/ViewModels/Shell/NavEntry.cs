using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One segment of the top bar's navigation (UI spec §3.2).</summary>
/// <param name="Page">The page it opens.</param>
/// <param name="Label">Its label.</param>
/// <param name="Key">The G-chord's second key (§8): H, W, Q, R, C, A, ..</param>
/// <param name="Count">The count from the page's view rows, or empty until its route is served (U0 serves none).</param>
internal sealed record NavEntry(PageId Page, string Label, string Key, string Count)
{
    /// <summary>True when there is a count to draw; an empty one takes no room (seven tabs must fit at 900 px).</summary>
    public bool HasCount => Count.Length > 0;
}
