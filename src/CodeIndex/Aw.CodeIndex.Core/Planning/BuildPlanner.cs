using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Planning;

/// <summary>Decides what a build has to do, as a pure function of data in and a plan out (spec §5.2).</summary>
public static class BuildPlanner
{
    private static readonly LayerName[] _layers = [LayerName.Structure, LayerName.Clones, LayerName.Deps, LayerName.Graph, LayerName.Embeddings];

    /// <summary>Plans a build.</summary>
    /// <param name="input">The snapshot, the live index's rows and the wanted stamps.</param>
    /// <returns>The files to parse and drop, and which layers are current.</returns>
    public static BuildPlan Plan(BuildPlanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stored = input.Stored.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var onDisk = input.Snapshot.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var layers = input.StoredLayers.ToDictionary(l => l.Layer);

        // A new parser, utility glob or scrub pattern changes every stored row, so the whole snapshot parses again.
        var all = !LayerHolds(layers, input.WantedStamps, LayerName.Structure);
        var toParse = input.Snapshot
            .Where(f => all || !stored.TryGetValue(f.Path, out var row) || !string.Equals(row.Blob, f.Blob, StringComparison.Ordinal))
            .ToList();
        var toDrop = stored.Keys.Where(p => !onDisk.Contains(p)).Order(StringComparer.Ordinal).ToList();

        var unchanged = toParse.Count == 0 && toDrop.Count == 0;
        return new BuildPlan(toParse, toDrop, _layers.ToDictionary(l => l, l => unchanged && LayerHolds(layers, input.WantedStamps, l)));
    }

    private static bool LayerHolds(Dictionary<LayerName, LayerRecord> stored, IReadOnlyDictionary<LayerName, string> wanted, LayerName layer)
        => stored.TryGetValue(layer, out var row)
            && wanted.TryGetValue(layer, out var stamp)
            && string.Equals(row.Stamp, stamp, StringComparison.Ordinal)
            && row.State is LayerState.Ok or LayerState.Disabled;
}
