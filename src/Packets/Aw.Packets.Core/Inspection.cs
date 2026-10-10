using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>A loaded packet and the outcome of every check.</summary>
/// <param name="Loaded">The packet as loaded.</param>
/// <param name="Checks">Every check outcome, in rule order.</param>
public sealed record Inspection(LoadedPacket Loaded, IReadOnlyList<CheckResult> Checks)
{
    /// <summary>True when no check failed (unchecked rules don't fail a packet).</summary>
    public bool Passed => Checks.All(static c => c.State is not (CheckState.Fail or CheckState.Omitted));
}
