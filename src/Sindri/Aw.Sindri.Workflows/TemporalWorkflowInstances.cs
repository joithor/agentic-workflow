using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Desk;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary><see cref="IWorkflowInstances"/> as a Temporal visibility query (engine spec rev 8 §5.1). Engine P0 spike.</summary>
internal sealed class TemporalWorkflowInstances : IWorkflowInstances
{
    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, string? temporal, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red");
}
