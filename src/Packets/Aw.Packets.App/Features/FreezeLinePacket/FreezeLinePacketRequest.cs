using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Features.FreezeLinePacket;

/// <summary>The FreezeLinePacket request.</summary>
/// <param name="Kind">A line kind's name: design, plan, phase, proof, review or handoff.</param>
/// <param name="PacketDirectory">The packet directory (holds packet.json).</param>
/// <param name="RepoDirectory">A directory inside the repository the packet is about; the packet directory when null.</param>
/// <param name="By">Who freezes it: the machine unless the owner asked.</param>
internal sealed record FreezeLinePacketRequest(string Kind, string PacketDirectory, string? RepoDirectory = null, FreezeBy By = FreezeBy.Machine);
