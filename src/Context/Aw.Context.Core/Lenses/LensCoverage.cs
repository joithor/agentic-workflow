using System.Text.Json.Serialization;

namespace Aw.Context.Core.Lenses;

/// <summary>How well the anchor's own scope is backed. The same record serves 'context map status --anchor' (D9), so the two agree field by field.</summary>
/// <param name="Scope">Nodes in the anchor's own scope.</param>
/// <param name="Intent">Scope nodes with a record or decision behind them.</param>
/// <param name="Product">Scope nodes that are not model-only.</param>
/// <param name="Code">Scope nodes a file confirmed-implements.</param>
/// <param name="Gaps">Scope nodes missing a lens.</param>
public sealed record LensCoverage(int Scope, CoverageIntent Intent, CoverageProduct Product, CoverageCode Code, IReadOnlyList<LensGap> Gaps)
{
    /// <summary>How well the map covers the files behind the scope; null on 'context map status --anchor' and inside <see cref="ByAnchor"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MapCoverage? Map { get; init; }

    /// <summary>Schema 2 only: each anchor's own coverage, keyed by anchor key (no nested 'byAnchor', no 'map').</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, LensCoverage>? ByAnchor { get; init; }
}
