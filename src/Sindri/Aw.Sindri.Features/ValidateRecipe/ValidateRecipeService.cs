using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Features.ValidateRecipe;

/// <summary>Reads the recipe, validates it against the build's catalog, and returns the findings and the hash. Red stub (P1 unit (a)).</summary>
internal sealed class ValidateRecipeService : IService<ValidateRecipeRequest, ValidateRecipeResponse>
{
    public ValidateRecipeService(IRecipeReader reader, IRecipeFiles files, BlockCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(catalog);
    }

    public ValueTask<Result<ValidateRecipeResponse>> Handle(ValidateRecipeRequest request, CancellationToken cancellationToken)
    {
        IChildRecipeResolver children = SameDirectoryChildren.None;
        throw new NotSupportedException($"red: recipe validate lands with the P1 units ({children.GetType().Name})");
    }
}
