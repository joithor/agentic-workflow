using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Packets.App.Features.FreezePacket;
using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.Core.Model;

namespace Aw.Packets.App;

/// <summary>The package's one source-generated JSON context (spec §4b.3): packet files, the freeze record and the CLI output.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(PacketManifest))]
[JsonSerializable(typeof(DecisionLog))]
[JsonSerializable(typeof(OutOfScope))]
[JsonSerializable(typeof(TaskPlan))]
[JsonSerializable(typeof(FreezeRecord))]
[JsonSerializable(typeof(ValidatePacketRequest))]
[JsonSerializable(typeof(ValidatePacketResponse))]
[JsonSerializable(typeof(FreezePacketRequest))]
[JsonSerializable(typeof(FreezePacketResponse))]
internal sealed partial class PacketsJsonContext : JsonSerializerContext;
