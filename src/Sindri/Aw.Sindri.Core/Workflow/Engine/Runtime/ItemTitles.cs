namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The titles of the items a list's instances are on, looked up once and shared by the list row and the list order, so the
/// label a row shows is the label it sorts by. The engine has no title source: the caller (the API read service, API P11)
/// supplies them; with none, an item is labelled by its id.
/// </summary>
public sealed class ItemTitles
{
    private readonly IReadOnlyDictionary<string, string> _titles;

    /// <summary>Creates the lookup.</summary>
    /// <param name="titles">The title of each item id.</param>
    public ItemTitles(IReadOnlyDictionary<string, string> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);
        _titles = titles;
    }

    /// <summary>No titles: every item is labelled by its id.</summary>
    public static ItemTitles None { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>The title of the item <paramref name="view"/> is on.</summary>
    /// <param name="view">The instance view.</param>
    /// <returns>The title, or null when the lookup has none for that item.</returns>
    public string? For(InstanceView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.ItemId is { } item && _titles.TryGetValue(item.Value, out var title) ? title : null;
    }
}
