namespace Aw.Context.Core.History;

/// <summary>Which lists were cut at their cap.</summary>
/// <param name="Records">Records.</param>
/// <param name="Decisions">Decisions.</param>
/// <param name="Changes">Changes.</param>
/// <param name="Files">Searched files.</param>
public sealed record HistoryTruncated(bool Records, bool Decisions, bool Changes, bool Files);
