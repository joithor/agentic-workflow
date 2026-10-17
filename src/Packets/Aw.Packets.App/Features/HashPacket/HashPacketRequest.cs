namespace Aw.Packets.App.Features.HashPacket;

/// <summary>The HashPacket request.</summary>
/// <param name="Kind">The packet kind's name ('build' or 'scoping').</param>
/// <param name="PacketDirectory">The packet directory.</param>
internal sealed record HashPacketRequest(string Kind, string PacketDirectory);
