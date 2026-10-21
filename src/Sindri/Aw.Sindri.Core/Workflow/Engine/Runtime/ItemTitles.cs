namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The titles of the items a list's instances are on, shared by the list row, the list order and the subject filter, so the
/// label a row shows is the label it sorts and filters by. The engine has no title source: the caller supplies them (the list
/// service reads them from the items view by key, scoped to the caller); with none, an item is labelled by its id.
/// </summary>
public sealed class ItemTitles
{
    private readonly IReadOnlyDictionary<string, string> _titles;
    private readonly IReadOnlyDictionary<string, string> _steps;

    /// <summary>Creates the lookup.</summary>
    /// <param name="titles">The title of each item id.</param>
    /// <param name="steps">The recipe step of each item id that has one; none when null.</param>
    public ItemTitles(IReadOnlyDictionary<string, string> titles, IReadOnlyDictionary<string, string>? steps = null)
    {
        ArgumentNullException.ThrowIfNull(titles);
        _titles = titles;
        _steps = steps ?? new Dictionary<string, string>(StringComparer.Ordinal);
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

    /// <summary>The recipe step of the item <paramref name="view"/> is on.</summary>
    /// <param name="view">The instance view.</param>
    /// <returns>The step, or null when the lookup has none for that item.</returns>
    public string? StepFor(InstanceView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.ItemId is { } item && _steps.TryGetValue(item.Value, out var step) ? step : null;
    }
}
