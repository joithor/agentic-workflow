namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/items/{id}/scope (api #503): the item's frozen scope, or none yet.</summary>
/// <param name="Item">The item's id.</param>
/// <param name="Scope">The frozen scope; null until the item's scoping has been approved and frozen.</param>
internal sealed record ItemScopeView(string Item, ScopeView? Scope);
