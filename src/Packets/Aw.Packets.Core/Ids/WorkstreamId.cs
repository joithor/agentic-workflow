using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Ids;

/// <summary>WorkstreamId: the id of a workstream in a packet's task plan.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<WorkstreamId>))]
public readonly partial struct WorkstreamId : ITypedId<WorkstreamId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("WorkstreamId must not be blank") : Validation.Ok;
}
