namespace Aw.Packets.App.Features.ValidatePacket;

/// <summary>The ValidatePacket request.</summary>
/// <param name="PacketDirectory">The packet directory (holds packet.json).</param>
/// <param name="RepoDirectory">A directory inside the repository the packet builds; the packet directory when null.</param>
/// <param name="SizesFile">The sizes file PKT-SIZE-001 checks every task against (LN-PKT3); null leaves the rule unchecked.</param>
internal sealed record ValidatePacketRequest(string PacketDirectory, string? RepoDirectory = null, string? SizesFile = null);
