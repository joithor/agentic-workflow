using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The app's view of Sindri's API (<see cref="ISindriApi"/>, continued): Run again.</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/workflow/instances/{instanceId}/rerun (RerunInstance, desk): starts the same recipe again on the same item with the same inputs, as the next attempt; sent once, never retried, no nonce.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="instanceId">The ended instance.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new instance, the API's refusal as served, or SND-UI-002/003.</returns>
    public Task<Result<StartedView>> RerunInstance(Uri api, string instanceId, CancellationToken cancellationToken);
}
