using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Ids;

/// <summary>ItemId: the id of an acceptance item in a line packet's acceptance.json.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ItemId>))]
public readonly partial struct ItemId : ITypedId<ItemId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("ItemId must not be blank") : Validation.Ok;
}
