using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Packets.App.Features.ValidatePacket;

namespace Aw.Packets.App;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(ValidatePacketRequest))]
[JsonSerializable(typeof(ValidatePacketResponse))]
internal sealed partial class PacketsJsonContext : JsonSerializerContext;
