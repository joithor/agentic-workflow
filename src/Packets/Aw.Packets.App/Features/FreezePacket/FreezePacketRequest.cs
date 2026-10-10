namespace Aw.Packets.App.Features.FreezePacket;

/// <summary>The FreezePacket request.</summary>
/// <param name="PacketDirectory">The packet directory (holds packet.json).</param>
/// <param name="RepoDirectory">A directory inside the repository the packet builds; the packet directory when null.</param>
internal sealed record FreezePacketRequest(string PacketDirectory, string? RepoDirectory = null);
