namespace Aw.Context.Core.Eval;

/// <summary>A finished probe run: its report and where it was written.</summary>
/// <param name="Report">The report.</param>
/// <param name="Path">The report file under $AW_SINDRI_HOME/context/eval/probes/; null when the run was incomplete and nothing was written.</param>
public sealed record ProbeRun(ProbeReport Report, string? Path)
{
    /// <summary>True when a probe's query failed: nothing was saved and the run is neither a regression nor a baseline.</summary>
    public bool Incomplete => Report.Incomplete;

    /// <summary>The ids of the probes whose query failed, in file order.</summary>
    public IReadOnlyList<ProbeId> Failed => [.. Report.Probes.Where(p => p.Error is not null).Select(p => p.Id)];
}
