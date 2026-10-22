using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Planning;

/// <summary>The planner's answer (spec §5.2).</summary>
/// <param name="ToParse">Snapshot files to parse and write, in snapshot order.</param>
/// <param name="ToDrop">Stored paths that left the snapshot, in ordinal order.</param>
/// <param name="LayersCurrent">Per layer, true when its stored row has the wanted stamp, is ok or disabled, and no file changed, so the build leaves it alone.</param>
public sealed record BuildPlan(
    IReadOnlyList<SnapshotFile> ToParse,
    IReadOnlyList<string> ToDrop,
    IReadOnlyDictionary<LayerName, bool> LayersCurrent);
