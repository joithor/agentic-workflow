using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One repo the caller sees, as the status words need it.</summary>
/// <param name="Repo">The repo's name.</param>
/// <param name="MapCommit">The commit the held or current map is for (the hold's head, else the current version's).</param>
/// <param name="IndexCommit">The commit the code index is at, or null when it is not indexed.</param>
/// <param name="Waiting">True while a held map waits on this repo's index.</param>
/// <param name="Word">While held, the per-repo word of the wait.</param>
/// <param name="RetryAt">While held and backing off, when the next build is due; otherwise null.</param>
/// <param name="BuiltAt">When the index at <paramref name="IndexCommit"/> was built, or null when the port does not say.</param>
internal sealed record PublishRepoFact(string Repo, string? MapCommit, string? IndexCommit, bool Waiting, RepoWait Word, DateTimeOffset? RetryAt = null, DateTimeOffset? BuiltAt = null);
