namespace Aw.Packets.App.Features.ValidateScopingPacket;

/// <summary>The ValidateScopingPacket request.</summary>
/// <param name="PacketDirectory">The scope packet directory.</param>
internal sealed record ValidateScopingPacketRequest(string PacketDirectory);
