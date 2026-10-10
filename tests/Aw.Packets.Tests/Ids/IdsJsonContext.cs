using System.Text.Json.Serialization;

namespace Aw.Packets.Tests.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(PacketIdHolder))]
[JsonSerializable(typeof(TaskIdHolder))]
[JsonSerializable(typeof(WorkstreamIdHolder))]
[JsonSerializable(typeof(QuestionIdHolder))]
[JsonSerializable(typeof(ExampleIdHolder))]
internal sealed partial class IdsJsonContext : JsonSerializerContext;
