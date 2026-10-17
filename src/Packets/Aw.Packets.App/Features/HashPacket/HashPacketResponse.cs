using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.HashPacket;

/// <summary>The HashPacket response, also the '--json' output (schemaVersion 1).</summary>
/// <param name="SchemaVersion">The output schema version.</param>
/// <param name="Kind">The packet kind.</param>
/// <param name="Hash">The canonical hash, 'sha256:&lt;hex&gt;'.</param>
/// <param name="DigestVersion">The canonical form's version.</param>
/// <param name="Files">The files the hash covers, relative to the packet directory, sorted by ordinal.</param>
internal sealed record HashPacketResponse(int SchemaVersion, string Kind, PacketHash Hash, int DigestVersion, IReadOnlyList<string> Files);
