namespace Aw.Sindri.Core.BugFix;

/// <summary>The check the baseline froze, as the instance's 'fact:check' holds it: bugfix-state runs the same one against a candidate.</summary>
/// <param name="Kind">'test' or 'ui'.</param>
/// <param name="File">The check file, relative to the working directory.</param>
/// <param name="Command">A test check's argv, as the baseline ran it; empty for a ui check.</param>
/// <param name="Directory">The working directory, relative to the repository root ('.' for the root).</param>
public sealed record FrozenCheck(string Kind, string File, IReadOnlyList<string> Command, string Directory);
