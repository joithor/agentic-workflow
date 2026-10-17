using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Packets.App.Features.FreezePacket;
using Aw.Packets.App.Features.FreezeScopingPacket;
using Aw.Packets.App.Features.HashPacket;
using Aw.Packets.App.Features.ValidateScopingPacket;
using Aw.Packets.Core.Kinds.Scoping;
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
[JsonSerializable(typeof(LensesFile))]
[JsonSerializable(typeof(ValidateScopingPacketRequest))]
[JsonSerializable(typeof(ValidateScopingPacketResponse))]
[JsonSerializable(typeof(HashPacketRequest))]
[JsonSerializable(typeof(HashPacketResponse))]
[JsonSerializable(typeof(FreezeScopingPacketRequest))]
[JsonSerializable(typeof(FreezeScopingPacketResponse))]
internal sealed partial class PacketsJsonContext : JsonSerializerContext;
