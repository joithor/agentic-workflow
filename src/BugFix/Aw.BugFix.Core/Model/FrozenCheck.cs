namespace Aw.BugFix.Core.Model;

/// <summary>The check that reproduced the bug, frozen at advance reproduce: every later run must execute exactly it.</summary>
/// <param name="Kind">A regression test or a ui-evidence script.</param>
/// <param name="Path">The check file, relative to the repository root, with '/'.</param>
/// <param name="Sha256">The check file's SHA-256 at the baseline.</param>
/// <param name="Command">The test command (argv) of the baseline run; null for a ui-evidence check.</param>
public sealed record FrozenCheck(CheckKind Kind, string Path, string Sha256, IReadOnlyList<string>? Command);
