using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>
/// 'recipe eval --shipped' (setup-once spec §5.3): runs the eval cases of every recipe in the embedded set over the pure engine, as 'recipe eval &lt;file&gt;' does for one file.
/// The publish runs it with the freshly published binary, and a failing case fails the publish.
/// </summary>
internal sealed class EvalShippedRecipesService(IEmbeddedRecipes embedded, IRecipeReader reader, BlockCatalog catalog, JsonSerializerOptions options)
    : IService<EvalShippedRequest, EvalShippedResponse>
{
    public ValueTask<Result<EvalShippedResponse>> Handle(EvalShippedRequest request, CancellationToken cancellationToken)
    {
        _ = (embedded, reader, catalog, options);
        throw new NotSupportedException("Not built yet.");
    }
}
