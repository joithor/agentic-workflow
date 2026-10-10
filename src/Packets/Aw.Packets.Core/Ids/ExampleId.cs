using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Ids;

/// <summary>ExampleId: the id of an acceptance example of a packet task.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ExampleId>))]
public readonly partial struct ExampleId : ITypedId<ExampleId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("ExampleId must not be blank") : Validation.Ok;
}
