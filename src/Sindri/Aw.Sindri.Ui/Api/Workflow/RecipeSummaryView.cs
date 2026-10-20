namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One row of GET /sindri/workflow/recipes: what the picker needs. The inputs come from the recipe's own read, not from here.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Version">The recipe version.</param>
/// <param name="Hash">The content hash, sha256:&lt;64 hex&gt;.</param>
/// <param name="Standing">'approved', 'unapproved' or 'revoked', as served; only an approved recipe starts.</param>
/// <param name="Name">The recipe's name template as written, or null.</param>
/// <param name="DisplayName">What the recipe does, in a sentence; read as optional (an older Sindri serves none).</param>
internal sealed record RecipeSummaryView(string Id, int Version, string Hash, string Standing, string? Name, string? DisplayName = null);
