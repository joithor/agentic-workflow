using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Desk;

namespace Aw.Sindri.Tests.ListInstances;

internal sealed class RecordingInstances(IReadOnlyList<InstanceRow> rows) : IWorkflowInstances
{
    public List<InstancesCall> Calls { get; } = [];

    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken)
    {
        Calls.Add(new InstancesCall(filter, InstanceFilters.ToQuery(filter), ended));
        return ValueTask.FromResult(Results.Success(rows));
    }

    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken)
    {
        Calls.Add(new InstancesCall(Filter: null, query, ended));
        return ValueTask.FromResult(Results.Success(rows));
    }
}
