using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>An approved recipe: its pinned version and its normalized long form.</summary>
/// <param name="Version">The id, version and approved hash.</param>
/// <param name="Recipe">The normalized recipe, its hash set.</param>
public sealed record ApprovedRecipe(RecipeVersion Version, Recipe Recipe);
