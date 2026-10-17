using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core;

/// <summary>A packet's canonical hash, the files it covers and which packet it is.</summary>
/// <param name="Hash">The canonical hash, 'sha256:&lt;hex&gt;'.</param>
/// <param name="Files">The files the hash covers, relative to the packet directory, sorted by ordinal.</param>
/// <param name="Identity">The repository and id from packet.json; null for a scope packet (it has none) or a line packet whose packet.json can't be read.</param>
public sealed record HashedPacket(PacketHash Hash, IReadOnlyList<string> Files, PacketIdentity? Identity);
