using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Design;

/// <summary>Opens a workflow.plan instance for a read the caller is allowed (line spec LN-API2): an unknown instance, or another repo's for a session caller, answers SND-ITEM-404 (one answer); an instance of another recipe SND-LINE-003.</summary>
internal sealed class PlanInstances
{
    /// <summary>Holds what opening needs.</summary>
    /// <param name="directory">Resolves the instance id to its address.</param>
    /// <param name="runtime">The instances on Temporal.</param>
    /// <param name="repos">Where each item belongs, from the ledger.</param>
    /// <param name="callers">The caller, whose repo scope decides what it may read.</param>
    public PlanInstances(IInstanceDirectory directory, IInstanceReader runtime, IItemRepos repos, ICallerContextAccessor callers)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(callers);
    }

    /// <summary>The instance's view, when the caller may read it and it runs the plan recipe.</summary>
    internal ValueTask<Result<InstanceView>> Open(WorkflowInstanceId instance, CancellationToken cancellationToken) => throw new NotSupportedException();
}
