namespace Aw.Context.Infrastructure.Git;

/// <summary>One commit of 'git log --first-parent'.</summary>
/// <param name="Sha">The commit.</param>
/// <param name="At">Its committer date.</param>
/// <param name="Parents">How many parents it has (2 or more for a merge).</param>
/// <param name="Subject">Its subject line.</param>
internal sealed record GitLogEntry(string Sha, DateTimeOffset At, int Parents, string Subject);
