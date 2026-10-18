using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The Start form's routes (<see cref="SindriApiClient"/>, continued): the recipe list and StartInstance.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<RecipeList>> Recipes(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<StartedView>> StartInstance(Uri api, StartInstanceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
