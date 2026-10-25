namespace Aw.Sindri.Core.BugFix;

/// <summary>An attempt bugfix-state opened.</summary>
/// <param name="Attempt">The attempt number (1-based).</param>
/// <param name="Mode">The attempt's mode.</param>
public sealed record AttemptStarted(int Attempt, AttemptMode Mode);
