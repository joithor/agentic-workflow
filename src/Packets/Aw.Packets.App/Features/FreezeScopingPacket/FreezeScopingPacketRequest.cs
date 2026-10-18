using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Features.FreezeScopingPacket;

/// <summary>The FreezeScopingPacket request.</summary>
/// <param name="PacketDirectory">The scope packet directory.</param>
/// <param name="RepoSlug">The repository the scoping instance belongs to, 'owner/name'.</param>
/// <param name="Instance">The scoping instance's slug (a packet id: lower-case letters, digits, '-').</param>
/// <param name="By">Who freezes it: the machine unless the owner asked.</param>
internal sealed record FreezeScopingPacketRequest(string PacketDirectory, string RepoSlug, string Instance, FreezeBy By = FreezeBy.Machine);
