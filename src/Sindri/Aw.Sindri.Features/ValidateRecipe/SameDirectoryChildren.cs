using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Features.ValidateRecipe;

/// <summary>The child recipes beside a recipe file (spec §9.1). Red stub (P1 unit (a)).</summary>
internal sealed class SameDirectoryChildren : IChildRecipeResolver
{
    public static SameDirectoryChildren None => new();

    public static ValueTask<SameDirectoryChildren> Load(string path, Recipe recipe, IRecipeReader reader, IRecipeFiles files, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: same-directory children land with P1 unit (a)");

    public ChildRecipe? Resolve(RecipeId id) => throw new NotSupportedException("red: same-directory children land with P1 unit (a)");
}
