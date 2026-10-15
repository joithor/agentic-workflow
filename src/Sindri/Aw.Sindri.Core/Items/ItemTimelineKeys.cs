using System.Globalization;

namespace Aw.Sindri.Core.Items;

/// <summary>The keys of the 'item-timeline' view: one row per event, ordered by item and then by ledger sequence.</summary>
public static class ItemTimelineKeys
{
    /// <summary>The key of the event at <paramref name="sequence"/> about <paramref name="item"/>.</summary>
    /// <param name="item">The item id.</param>
    /// <param name="sequence">The ledger sequence.</param>
    /// <returns>'&lt;item&gt;/&lt;sequence:D19&gt;'.</returns>
    public static string Of(string item, long sequence)
        => string.Create(CultureInfo.InvariantCulture, $"{item}/{sequence:D19}");

    /// <summary>The exclusive upper bound of every key of <paramref name="item"/> ('/' sorts just before '0').</summary>
    /// <param name="item">The item id.</param>
    /// <returns>The bound.</returns>
    public static string EndOf(string item) => item + "0";
}
