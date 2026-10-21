using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The commands on a workflow instance (<see cref="SindriApiClient"/>, continued): a desk report.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<CompleteStepAccepted>> CompleteStep(Uri api, string instanceId, CompleteStepBody body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
