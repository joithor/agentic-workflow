using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The revoke and drift wake until P4 brings the signal sender (engine spec rev 8, Phase 2): no instance runs on
/// Temporal yet, so it finds none and sends nothing.
/// </summary>
internal sealed class NoOpRecipeWake : IRecipeWake
{
    public ValueTask<Result<IReadOnlyList<WorkflowInstanceId>>> Running(RecipeHash hash, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<WorkflowInstanceId>>>(Array.Empty<WorkflowInstanceId>());

    public ValueTask<Result<Unit>> Wake(RecipeWakeNotice notice, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
