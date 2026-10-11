namespace Aw.BugFix.Core.Model;

/// <summary>The change from the baseline to a candidate.</summary>
/// <param name="Stat">git diff --stat.</param>
/// <param name="Full">The full diff without lock files and build output; null when git couldn't produce it.</param>
public sealed record CommitDiff(string Stat, string? Full);
