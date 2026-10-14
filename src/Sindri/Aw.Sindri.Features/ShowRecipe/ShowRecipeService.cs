using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.ShowRecipe;

/// <summary>
/// A registered recipe's line and the preview approving it would show (engine spec §4.2, §6.3 'recipe show &lt;id@hash&gt;'),
/// by the full hash or a prefix of 8 or more digits, whether it is unapproved, approved or revoked.
/// </summary>
/// <param name="planner">Finds the recipe and builds the approval preview.</param>
/// <param name="registry">Reads where the recipe stands.</param>
internal sealed class ShowRecipeService(RecipeApprovalPlanner planner, RecipeRegistryReader registry) : IService<ShowRecipeRequest, ShowRecipeResponse>
{
    public async ValueTask<Result<ShowRecipeResponse>> Handle(ShowRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await planner.Find(request.Id, request.Hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var line = await registry.Summary(found.Value, cancellationToken).ConfigureAwait(false);
        var preview = line.IsError ? line.Error : await planner.Preview(found.Value, cancellationToken).ConfigureAwait(false);
        return preview.IsError ? preview.Error : new ShowRecipeResponse(line.Value, preview.Value);
    }
}
