namespace Aw.Sindri.Core.Views;

/// <summary>The view tables (a fixed set: an adapter never builds a table name from input).</summary>
public static class ViewNames
{
    /// <summary>Each service's last run.</summary>
    public const string Services = "services";

    /// <summary>The approved and live profile.</summary>
    public const string Profile = "profile";

    /// <summary>Work items, keyed by item id.</summary>
    public const string Items = "items";

    /// <summary>Tracker aliases, keyed by alias: the item each one names.</summary>
    public const string ItemAliases = "item-aliases";

    /// <summary>Each item's timeline, keyed '&lt;itemId&gt;/&lt;sequence:D19&gt;'.</summary>
    public const string ItemTimeline = "item-timeline";

    /// <summary>Every view.</summary>
    public static IReadOnlyList<string> All { get; } = [Services, Profile, Items, ItemAliases, ItemTimeline];
}
