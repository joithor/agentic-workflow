using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Ids;

/// <summary>TaskId: the id of a task in a packet's task plan.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<TaskId>))]
public readonly partial struct TaskId : ITypedId<TaskId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("TaskId must not be blank") : Validation.Ok;
}
