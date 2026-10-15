using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>
/// The instances, by a visibility query on their search attributes (engine spec rev 8 §5.1, §6.3 'workflow list').
/// Engine P4a; unit (e) lists ended instances too when asked, for 'list --item' and '--state'.
/// </summary>
public interface IWorkflowInstances
{
    /// <summary>Lists the instances that match <paramref name="query"/>: the running ones, and the ended ones too when <paramref name="ended"/>.</summary>
    /// <param name="query">A visibility query on the search attributes ('NeedsYou = true'), or null for every instance.</param>
    /// <param name="ended">Whether ended instances are listed as well.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The instances, by address.</returns>
    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken);
}
