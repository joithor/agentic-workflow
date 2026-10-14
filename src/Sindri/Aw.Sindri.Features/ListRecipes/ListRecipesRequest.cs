using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>List the registered recipes (engine spec §6.3 'recipe list [--stale]', GET /workflow/recipes; query).</summary>
/// <param name="Stale">True for only the approved recipes whose pins no longer match the build, or whose pinned child is stale.</param>
public sealed record ListRecipesRequest(bool Stale) : IQuery<ListRecipesResponse>;
