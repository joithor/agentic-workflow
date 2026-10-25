namespace Aw.Sindri.Core.Scheduling;

/// <summary>The commit a map build read for one repo.</summary>
/// <param name="Commit">The commit.</param>
/// <param name="OnDefaultBranch">Whether it was on the repo's default branch.</param>
public sealed record MapRepoHead(string Commit, bool OnDefaultBranch);
