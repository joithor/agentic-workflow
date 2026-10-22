namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Where a repo's code answers stand against the map: the index's commit first, then the map's.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="IndexCommit">The commit the code index is at (7 hex).</param>
/// <param name="MapCommit">The commit the map is for (7 hex).</param>
internal sealed record StaleRepo(string Repo, string IndexCommit, string MapCommit);
