namespace Aw.Context.Core.Proposals;

/// <summary>'GET map/proposals?run=': the graph status of what one scoping run proposed.</summary>
/// <param name="Run">The run.</param>
/// <param name="Map">The current map read ('v13@7c3e1a2').</param>
/// <param name="Draft">The draft read, or null when there is none.</param>
/// <param name="Surfaces">Each surface the run proposed and its status.</param>
public sealed record ProposalsView(string Run, string Map, string? Draft, IReadOnlyList<GraphStatus> Surfaces);
