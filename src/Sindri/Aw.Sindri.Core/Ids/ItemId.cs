using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// A work item's id (API spec §3): 'ITM-' and 32 lowercase hex digits, a GUIDv7 in 'N' format. Responses always carry it;
/// a route may name the item by its tracker alias instead (<see cref="ItemRef"/>).
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ItemId>))]
public readonly partial struct ItemId : ITypedId<ItemId, string>
{
    private const string Prefix = "ITM-";

    /// <summary>A new item id from the id port.</summary>
    /// <param name="ids">The id port (a GUIDv7 in production, a sequence in tests).</param>
    /// <returns>The id.</returns>
    public static ItemId New(IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return From(Prefix + ids.NewId().ToString("N", CultureInfo.InvariantCulture));
    }

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An item id is 'ITM-' and 32 lowercase hex digits");

    [GeneratedRegex("^ITM-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
