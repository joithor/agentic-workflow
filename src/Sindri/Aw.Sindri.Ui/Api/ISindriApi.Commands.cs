using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The commands on a workflow instance (<see cref="ISindriApi"/>, continued): a desk report.</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/workflow/instances/{id}/runs/{run}/complete: the report for an open desk run. A human caller; sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="instanceId">The instance's id.</param>
    /// <param name="body">The run, the outcome and the outputs.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted report, the API's refusal (the run is not open, an undeclared outcome, SND-AUTH-002), or SND-UI-002/003.</returns>
    public Task<Result<CompleteStepAccepted>> CompleteStep(Uri api, string instanceId, CompleteStepBody body, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflow/instances/{id}/commands: a protected answer ('--grant-tokens n') with the challenge the person answered. A human caller; sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="instanceId">The instance's id.</param>
    /// <param name="body">The answer and its challenge.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The applied command, the API's refusal (SND-WF-003 outside the bound, a spent or stale challenge, SND-AUTH-002), or SND-UI-002/003.</returns>
    public Task<Result<CommandApplied>> ApplyCommand(Uri api, string instanceId, ApplyCommandBody body, CancellationToken cancellationToken);
}
