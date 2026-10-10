namespace Aw.BugFix.Core.Model;

/// <summary>One recorded step.</summary>
/// <param name="At">When.</param>
/// <param name="Command">The command (e.g. "advance reproduce", "record-candidate c1").</param>
/// <param name="From">The phase before; null for init.</param>
/// <param name="To">The phase after.</param>
/// <param name="Evidence">The file the step rests on, if any.</param>
public sealed record HistoryEntry(DateTimeOffset At, string Command, Phase? From, Phase To, string? Evidence);
