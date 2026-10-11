using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A recipe named by id and hash ('id@hash'), as a generated recipe names its base.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">Its hash.</param>
public sealed record RecipeRef(RecipeId Id, RecipeHash Hash);
