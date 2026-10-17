using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Ids;

namespace Aw.Packets.App.Features.ValidateLinePacket;

/// <summary>The ValidateLinePacket response, also the '--json' output (schemaVersion 1).</summary>
/// <param name="SchemaVersion">The output schema version.</param>
/// <param name="Kind">The packet kind.</param>
/// <param name="Packet">The packet id; null when packet.json can't be read.</param>
/// <param name="Passed">True when no check failed (unchecked ones don't fail it).</param>
/// <param name="Checks">Every check outcome: one record per failure, one per passing or unchecked rule.</param>
internal sealed record ValidateLinePacketResponse(int SchemaVersion, string Kind, PacketId? Packet, bool Passed, IReadOnlyList<CheckResult> Checks);
