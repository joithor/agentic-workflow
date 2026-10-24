using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The agent runs of a scoping instance from the ledger (issue 966, spec 6): 'launch.started' joined to 'session.usage' by launch id, and a completion at the desk where a run has no launch.</summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
/// <param name="instances">The instance, for the recipe it runs.</param>
/// <param name="recipes">The approved recipes, for each state's Step and requested model.</param>
internal sealed class ScopeRunRecords(ILedgerReader ledger, EventFactory events, IInstanceReader instances, IApprovedRecipes recipes) : IScopeRunRecords
{
    public ValueTask<Result<IReadOnlyList<ScopeRunRecord>>> Of(InstanceAddress run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(recipes);
        throw new NotSupportedException();
    }
}
