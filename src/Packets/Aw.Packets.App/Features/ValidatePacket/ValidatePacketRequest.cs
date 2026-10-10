namespace Aw.Packets.App.Features.ValidatePacket;

/// <summary>The ValidatePacket request.</summary>
/// <param name="PacketDirectory">The packet directory (holds packet.json).</param>
/// <param name="RepoDirectory">A directory inside the repository the packet builds; the packet directory when null.</param>
internal sealed record ValidatePacketRequest(string PacketDirectory, string? RepoDirectory = null);
