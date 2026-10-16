using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// What an '{itemId}' route segment or an '?item=' value holds (API spec §3.1): the item's id, or a tracker alias it is
/// linked under. Exactly one of the two is set; the item resolver turns either into the item. Written as its text.
/// </summary>
/// <param name="Id">The item id, when the text is one.</param>
/// <param name="Alias">The alias, when the text is one.</param>
[JsonConverter(typeof(ItemRefJsonConverter))]
public readonly record struct ItemRef(ItemId? Id, ItemAlias? Alias)
{
    /// <summary>A reference by id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The reference.</returns>
    public static ItemRef To(ItemId id) => new(id, Alias: null);

    /// <summary>A reference by alias.</summary>
    /// <param name="alias">The alias.</param>
    /// <returns>The reference.</returns>
    public static ItemRef To(ItemAlias alias) => new(Id: null, alias);

    /// <summary>Reads an item id ('ITM-…') or, failing that, an alias ('ENG-142').</summary>
    /// <param name="text">The route or query value.</param>
    /// <param name="value">The reference, when the text is either.</param>
    /// <returns>True when it is.</returns>
    public static bool TryParse(string? text, out ItemRef value)
    {
        if (ItemId.TryFrom(text ?? string.Empty, out var id))
        {
            value = To(id);
            return true;
        }

        if (ItemAlias.TryFrom(text ?? string.Empty, out var alias))
        {
            value = To(alias);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>The text it was read from: the id, else the alias.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => Id?.Value ?? Alias?.Value ?? string.Empty;
}
