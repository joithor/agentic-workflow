using Aw.Packets.Core.Checks;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>A line packet's checks, and what the packet.json said when it could be read.</summary>
/// <param name="Kind">The kind checked.</param>
/// <param name="Files">The packet as read.</param>
/// <param name="Manifest">packet.json, or null when it is missing or doesn't match the kind's schema.</param>
/// <param name="Checks">Every check outcome, in rule order.</param>
public sealed record LineInspection(PacketKind Kind, LinePacketFiles Files, LineManifest? Manifest, IReadOnlyList<CheckResult> Checks)
{
    /// <summary>True when no check failed (unchecked rules don't fail a packet).</summary>
    public bool Passed => Checks.All(static c => c.State is not (CheckState.Fail or CheckState.Omitted));
}
