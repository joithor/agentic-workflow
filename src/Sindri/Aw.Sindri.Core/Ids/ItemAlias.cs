using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// A tracker's key for a linked item, such as 'ENG-142' (Linear's shape): an upper-case team of 1 to 10 letters or
/// digits starting with a letter, a hyphen and a number without a leading zero. A route may name an item by it; it is
/// never the item's id.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ItemAlias>))]
public readonly partial struct ItemAlias : ITypedId<ItemAlias, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An item alias is a tracker key such as 'ENG-142'");

    [GeneratedRegex("^[A-Z][A-Z0-9]{0,9}-[1-9][0-9]{0,8}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
