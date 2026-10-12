using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>No pinned child recipes. Engine P0 spike: 'workflow.step' has no child wait; P4 resolves the approval's pins.</summary>
internal sealed class NoChildRecipes : IChildRecipeResolver
{
    /// <summary>The one instance.</summary>
    public static NoChildRecipes Instance { get; } = new();

    public ChildRecipe? Resolve(RecipeId id) => null;
}
