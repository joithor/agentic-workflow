using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Ids;

namespace Aw.Packets.App.Features.ValidatePacket;

/// <summary>The ValidatePacket response, also the '--json' output (schemaVersion 1).</summary>
/// <param name="SchemaVersion">The output schema version.</param>
/// <param name="Packet">The packet id.</param>
/// <param name="Passed">True when no check failed (unchecked ones don't fail it).</param>
/// <param name="Checks">Every check outcome: one record per failure, one per passing or unchecked rule.</param>
internal sealed record ValidatePacketResponse(int SchemaVersion, PacketId Packet, bool Passed, IReadOnlyList<CheckResult> Checks);
