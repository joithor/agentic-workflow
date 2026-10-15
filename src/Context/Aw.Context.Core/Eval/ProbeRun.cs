namespace Aw.Context.Core.Eval;

/// <summary>A finished probe run: its report and where it was written.</summary>
/// <param name="Report">The report.</param>
/// <param name="Path">The report file under $AW_SINDRI_HOME/context/eval/probes/; null when the run was incomplete and nothing was written.</param>
public sealed record ProbeRun(ProbeReport Report, string? Path);
