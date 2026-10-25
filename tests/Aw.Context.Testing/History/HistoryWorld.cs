using Aw.Context.Core.History;

namespace Aw.Context.Testing.History;

/// <summary>A repo's first-parent history as <see cref="FileHistoryContract"/> expects to read it back.</summary>
/// <param name="Head">The full sha of HEAD.</param>
/// <param name="Commits">Every first-parent commit, newest first, each with every path it changed against its first parent.</param>
public sealed record HistoryWorld(string Head, IReadOnlyList<FileChange> Commits)
{
    /// <summary>The commit with this subject.</summary>
    /// <param name="subject">A subject from <see cref="HistoryScript"/>.</param>
    /// <returns>The commit.</returns>
    public FileChange Find(string subject) => Commits.Single(c => string.Equals(c.Subject, subject, StringComparison.Ordinal));
}
