using System.Text.Json.Serialization;

namespace Aw.Packets.Tests.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(PacketIdHolder))]
[JsonSerializable(typeof(TaskIdHolder))]
[JsonSerializable(typeof(WorkstreamIdHolder))]
[JsonSerializable(typeof(QuestionIdHolder))]
[JsonSerializable(typeof(ExampleIdHolder))]
[JsonSerializable(typeof(RepoSlugHolder))]
[JsonSerializable(typeof(CommitShaHolder))]
[JsonSerializable(typeof(PacketHashHolder))]
[JsonSerializable(typeof(OptionalPacketHashHolder))]
internal sealed partial class IdsJsonContext : JsonSerializerContext;
