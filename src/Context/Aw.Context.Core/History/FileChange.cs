namespace Aw.Context.Core.History;

/// <summary>One first-parent commit that touched a searched path. Only its subject line is ever read, never a body.</summary>
/// <param name="Sha">The full commit sha.</param>
/// <param name="At">The committer date.</param>
/// <param name="Subject">The first line of the message, as git has it (not yet scrubbed).</param>
/// <param name="Parents">How many parents it has (2 or more for a merge).</param>
/// <param name="Files">The searched paths it changed against its first parent, in ordinal order.</param>
public sealed record FileChange(string Sha, DateTimeOffset At, string Subject, int Parents, IReadOnlyList<string> Files);
