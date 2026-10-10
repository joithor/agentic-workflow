using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Kernel.Tests.TypedIds;

/// <summary>A string id: an item in a backlog.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ItemId>))]
internal readonly partial struct ItemId : ITypedId<ItemId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("ItemId must not be blank") : Validation.Ok;
}
