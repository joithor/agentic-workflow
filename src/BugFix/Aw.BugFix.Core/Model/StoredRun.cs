namespace Aw.BugFix.Core.Model;

/// <summary>A run record as read back from disk, with where it is and its current hash.</summary>
/// <param name="Path">The record's full path.</param>
/// <param name="Sha256">Its SHA-256 now.</param>
/// <param name="Record">The record.</param>
public sealed record StoredRun(string Path, string Sha256, RunRecord Record);
