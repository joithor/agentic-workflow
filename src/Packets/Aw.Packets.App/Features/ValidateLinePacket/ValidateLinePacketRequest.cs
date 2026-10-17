namespace Aw.Packets.App.Features.ValidateLinePacket;

/// <summary>The ValidateLinePacket request.</summary>
/// <param name="Kind">A line kind's name: design, plan, phase, proof, review or handoff.</param>
/// <param name="PacketDirectory">The packet directory (holds packet.json).</param>
/// <param name="RepoDirectory">A directory inside the repository the packet is about; the packet directory when null.</param>
internal sealed record ValidateLinePacketRequest(string Kind, string PacketDirectory, string? RepoDirectory = null);
