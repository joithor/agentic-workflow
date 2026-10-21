namespace Aw.Context.Core.History;

/// <summary>What the lens found before the caps, and what the 'before' cut dropped.</summary>
/// <param name="Records">Records after the cut, before the cap.</param>
/// <param name="Decisions">Decisions after the cut, before the cap.</param>
/// <param name="Changes">Changes before the cap.</param>
/// <param name="Files">Implementing files searched.</param>
/// <param name="Unlocated">Implementing files whose repo the map doesn't name.</param>
/// <param name="AfterCut">Records and decisions the 'before' cut dropped.</param>
public sealed record HistoryCounts(int Records, int Decisions, int Changes, int Files, int Unlocated, int AfterCut);
