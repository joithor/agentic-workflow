namespace Aw.Evidence.Core.Runs;

/// <summary>The run's recordings (absolute paths).</summary>
/// <param name="Traces">One trace archive per viewport.</param>
/// <param name="Videos">The session videos.</param>
public sealed record RunEvidence(IReadOnlyList<string> Traces, IReadOnlyList<string> Videos);
