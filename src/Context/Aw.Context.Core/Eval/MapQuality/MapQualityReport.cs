namespace Aw.Context.Core.Eval.MapQuality;

/// <summary>The map's quality on the owner's own decisions (spec §9.2 item 2), kept under $AW_SINDRI_HOME/context/eval/map/.</summary>
/// <param name="SchemaVersion">The report format.</param>
/// <param name="CreatedAt">When it was measured.</param>
/// <param name="MapVersion">The map version it read ('v3').</param>
/// <param name="NodePrecision">Accepted over accepted plus rejected node proposals, all provenances.</param>
/// <param name="NodePrecisionDeterministic">The same for deterministic proposals.</param>
/// <param name="NodePrecisionModel">The same for model proposals.</param>
/// <param name="LinkPrecision">Accepted over accepted plus rejected suggested file links.</param>
/// <param name="Undecided">Proposed nodes with no accept or reject yet: counted apart, never as misses.</param>
/// <param name="UnmappedRate">Files changed in the window that implement no node, over files changed in it.</param>
/// <param name="Window">The window the unmapped rate looked at ('2026-04-30..2026-07-29', 90 days before the mapped commit), or null.</param>
/// <param name="Bars">The §9.4 verdicts: 'node precision ≥ 0.80: met', 'unmapped rate ≤ 0.20: not measured'.</param>
public sealed record MapQualityReport(int SchemaVersion, DateTimeOffset CreatedAt, string MapVersion, Ratio NodePrecision, Ratio NodePrecisionDeterministic, Ratio NodePrecisionModel,
    Ratio LinkPrecision, int Undecided, Ratio UnmappedRate, string? Window, IReadOnlyList<string> Bars)
{
    /// <summary>The report format this build writes.</summary>
    public const int CurrentSchemaVersion = 1;
}
