namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A repo whose code answers are from another commit than the map's.</summary>
/// <param name="Repo">Repo, as served.</param>
/// <param name="IndexCommit">IndexCommit, as served: the commit the code index is at (7 hex).</param>
/// <param name="MapCommit">MapCommit, as served: the commit the map is for (7 hex).</param>
internal sealed record MapPublishStaleRepo(string Repo, string IndexCommit, string MapCommit);
