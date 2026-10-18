using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.VerifyPacket;

/// <summary>The VerifyPacket request.</summary>
/// <param name="Kind">The packet kind's name: build, scoping, or a line kind.</param>
/// <param name="PacketDirectory">The packet directory.</param>
/// <param name="Hash">The hash the packet is expected to have, 'sha256:&lt;hex&gt;'.</param>
/// <param name="Candidate">True to only recompute and compare, without looking for a freeze record.</param>
/// <param name="RepoSlug">A scope packet's repository, 'owner/name' (the other kinds read it from packet.json); needed unless <paramref name="Candidate"/>.</param>
/// <param name="Instance">A scope packet's scoping instance (the other kinds read their id from packet.json); needed unless <paramref name="Candidate"/>.</param>
internal sealed record VerifyPacketRequest(string Kind, string PacketDirectory, PacketHash Hash, bool Candidate = false, string? RepoSlug = null, string? Instance = null);
