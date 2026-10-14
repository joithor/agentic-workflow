namespace Aw.Context.Core.Eval;

/// <summary>One probe run (spec §9.2 item 3), kept under $AW_SINDRI_HOME/context/eval/probes/.</summary>
/// <param name="SchemaVersion">The report format (<see cref="CurrentSchemaVersion"/>).</param>
/// <param name="ProbeFile">The probe file's path.</param>
/// <param name="ProbeFileHash">The probe file's hash; only runs of the same file are compared.</param>
/// <param name="MapVersion">The map version the answers read (the first answer's), or null.</param>
/// <param name="Probes">Each probe's score, in file order.</param>
/// <param name="Totals">This run's means.</param>
/// <param name="Previous">The previous run of the same file, or null for the first.</param>
/// <param name="Regressed">True when a mean is lower than <paramref name="Previous"/>'s (SND-CONTEXT-038).</param>
public sealed record ProbeReport(int SchemaVersion, string ProbeFile, string ProbeFileHash, string? MapVersion, IReadOnlyList<ProbeScore> Probes,
    ProbeTotals Totals, ProbeTotals? Previous, bool Regressed)
{
    /// <summary>The report format this build writes.</summary>
    public const int CurrentSchemaVersion = 1;
}
