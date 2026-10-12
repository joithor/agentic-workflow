using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>The open instances, by a visibility query on their search attributes (engine spec rev 8 §5.1, §6.3 'workflow list'). Engine P0 spike.</summary>
public interface IWorkflowInstances
{
    /// <summary>Lists the running instances that match <paramref name="query"/>.</summary>
    /// <param name="query">A visibility query on the search attributes ('NeedsYou = true'), or null for every open instance.</param>
    /// <param name="temporal">The Temporal frontend ('host:port'), or null for the one control.json records.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The instances, by address.</returns>
    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, string? temporal, CancellationToken cancellationToken);
}
