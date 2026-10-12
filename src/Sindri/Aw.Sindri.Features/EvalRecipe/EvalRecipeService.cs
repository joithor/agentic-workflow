using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>'recipe eval' (spec §6.3, §9.2, W27) over the recipe reader, the recipe files, the build's catalog and the JSON options. Stub: P3 unit (f) implements it.</summary>
internal sealed class EvalRecipeService(IRecipeReader reader, IRecipeFiles files, BlockCatalog catalog, JsonSerializerOptions options)
    : IService<EvalRecipeRequest, EvalRecipeResponse>
{
    public ValueTask<Result<EvalRecipeResponse>> Handle(EvalRecipeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"recipe eval lands with P3 unit (f) ({reader.GetType().Name}, {files.GetType().Name}, {catalog.Blocks.Count}, {options.PropertyNamingPolicy})");
}
