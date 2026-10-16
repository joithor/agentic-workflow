using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Desk;

namespace Aw.Sindri.Tests.ListInstances;

internal sealed class RecordingInstances(IReadOnlyList<InstanceRow> rows, IReadOnlyCollection<string>? running = null) : IWorkflowInstances
{
    public List<InstancesCall> Calls { get; } = [];

    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken)
    {
        Calls.Add(new InstancesCall(filter, InstanceFilters.ToQuery(filter), ended));
        return ValueTask.FromResult(Results.Success(Listed(ended)));
    }

    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken)
    {
        Calls.Add(new InstancesCall(Filter: null, query, ended));
        return ValueTask.FromResult(Results.Success(Listed(ended)));
    }

    /// <summary>Every row, or with running given and no ended instances asked for, the running ones only (as the visibility query does).</summary>
    private IReadOnlyList<InstanceRow> Listed(bool ended) => ended || running is null ? rows : [.. rows.Where(r => running.Contains(r.Address, StringComparer.Ordinal))];
}
