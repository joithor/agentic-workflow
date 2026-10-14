using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>The recipes by id and version; empty prints 'no recipes registered'.</summary>
/// <param name="Recipes">The lines.</param>
public sealed record ListRecipesResponse(IReadOnlyList<RecipeSummary> Recipes);
