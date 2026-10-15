namespace Aw.Meta.Core.Lanes;

/// <summary>The lane map and its version; the version rises whenever the assignments change.</summary>
/// <param name="Version">The version, 0 before any lane is mapped.</param>
/// <param name="Entries">The assignments, in lane order.</param>
public sealed record LaneMapState(int Version, IReadOnlyList<LaneAssignment> Entries)
{
    /// <summary>The map before any lane is known.</summary>
    public static LaneMapState Empty { get; } = new(0, []);
}
