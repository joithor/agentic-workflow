namespace Aw.Context.Core.Eval;

/// <summary>One probe run (spec §9.2 item 3), kept under $AW_SINDRI_HOME/context/eval/probes/.</summary>
/// <param name="SchemaVersion">The report format (<see cref="CurrentSchemaVersion"/>).</param>
/// <param name="ProbeFile">The probe file's path.</param>
/// <param name="ProbeFileHash">The probe file's hash; only runs of the same file are compared.</param>
/// <param name="MapVersion">The map version the answers read (the first answer's), or null.</param>
/// <param name="Probes">Each probe's score, in file order.</param>
/// <param name="Totals">This run's means.</param>
/// <param name="Baseline">The baseline this run was compared with: the newest complete run of the same file that did not regress (not necessarily the one just before), or null when there is none yet. Reports written before the rename carry it as 'previous' and still read.</param>
/// <param name="Regressed">True when a mean is lower than <paramref name="Baseline"/>'s, or a probe returned more absent nodes than it did in the baseline (SND-CONTEXT-038); never true for an incomplete run.</param>
public sealed record ProbeReport(int SchemaVersion, string ProbeFile, string ProbeFileHash, string? MapVersion, IReadOnlyList<ProbeScore> Probes,
    ProbeTotals Totals, ProbeTotals? Baseline, bool Regressed)
{
    /// <summary>The report format this build writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>True when any probe's query failed: the run says nothing about retrieval, so it is neither a regression nor a baseline, and it is not saved.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Incomplete => Probes.Any(p => p.Error is not null);

    /// <summary>True when a later run may be compared with this one: it is complete and did not regress.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsBaseline => !Regressed && !Incomplete;
}
