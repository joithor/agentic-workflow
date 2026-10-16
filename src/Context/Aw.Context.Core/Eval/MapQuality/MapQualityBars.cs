namespace Aw.Context.Core.Eval.MapQuality;

/// <summary>The §9.4 map bars and how a ratio is judged against them. Pure.</summary>
public static class MapQualityBars
{
    /// <summary>Map node precision at least this.</summary>
    public const double NodePrecision = 0.80;

    /// <summary>Unmapped rate at most this.</summary>
    public const double UnmappedRate = 0.20;

    /// <summary>Judges the report's two bars.</summary>
    /// <param name="nodePrecision">The node precision.</param>
    /// <param name="unmapped">The unmapped rate.</param>
    /// <returns>The two verdict lines.</returns>
    public static IReadOnlyList<string> Judge(Ratio nodePrecision, Ratio unmapped)
    {
        ArgumentNullException.ThrowIfNull(nodePrecision);
        ArgumentNullException.ThrowIfNull(unmapped);
        return
        [
            "node precision ≥ 0.80: " + (nodePrecision.Value is { } p ? (p >= NodePrecision ? "met" : "not met") : "not measured"),
            "unmapped rate ≤ 0.20: " + (unmapped.Value is { } u ? (u <= UnmappedRate ? "met" : "not met") : "not measured"),
        ];
    }
}
