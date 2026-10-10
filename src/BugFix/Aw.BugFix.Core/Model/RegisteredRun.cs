namespace Aw.BugFix.Core.Model;

/// <summary>A run record run-test or run-ui wrote; evidence counts only when it is registered here and unmodified.</summary>
/// <param name="Evidence">The run record's full path.</param>
/// <param name="Sha256">Its SHA-256 when written.</param>
public sealed record RegisteredRun(string Evidence, string Sha256);
