using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Planning;

/// <summary>What the planner compares (spec §5.2): the snapshot, the live index's file rows and layer rows, and the stamps this build wants.</summary>
/// <param name="Snapshot">The snapshot's files with their blob ids.</param>
/// <param name="Stored">The live index's file rows; empty for a first build or one that starts over.</param>
/// <param name="StoredLayers">The live index's layer rows; empty for a first build or one that starts over.</param>
/// <param name="WantedStamps">The stamp each layer would get now; a layer missing here is never current. The structure stamp comes from <see cref="StructureStamp"/>.</param>
public sealed record BuildPlanInput(
    IReadOnlyList<SnapshotFile> Snapshot,
    IReadOnlyList<StoredFile> Stored,
    IReadOnlyList<LayerRecord> StoredLayers,
    IReadOnlyDictionary<LayerName, string> WantedStamps);
