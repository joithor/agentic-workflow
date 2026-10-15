using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// One segment of the top bar's navigation (UI spec §3.2). The entry keeps its identity when its count changes, so the
/// list's selection (the current page's highlight) is never dropped by a swap.
/// </summary>
internal sealed class NavEntry(PageId page, string label, string key, string count) : ObservableObject
{
    /// <summary>The page it opens.</summary>
    public PageId Page { get; } = page;

    /// <summary>Its label.</summary>
    public string Label { get; } = label;

    /// <summary>The G-chord's second key (§8): H, Q, D, S.</summary>
    public string Key { get; } = key;

    /// <summary>The count from the page's view rows, or empty until its route is served.</summary>
    public string Count { get; internal set => Set(ref field, value); } = count;
}
