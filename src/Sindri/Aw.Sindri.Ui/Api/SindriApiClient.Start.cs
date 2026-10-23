using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The Start form's routes (<see cref="SindriApiClient"/>, continued): the recipe list and StartInstance.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<RecipeList>> Recipes(Uri api, CancellationToken cancellationToken) => Get<RecipeList>(api, WorkflowRoutes.Recipes, cancellationToken);

    /// <inheritdoc />
    public Task<Result<StartedView>> StartInstance(Uri api, StartInstanceRequest request, CancellationToken cancellationToken) =>
        Call<StartedView>(api, HttpMethod.Post, WorkflowRoutes.Start, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<StartedView>> RerunInstance(Uri api, string instanceId, CancellationToken cancellationToken) =>
        Call<StartedView>(api, HttpMethod.Post, WorkflowRoutes.Rerun(instanceId), cancellationToken);
}
