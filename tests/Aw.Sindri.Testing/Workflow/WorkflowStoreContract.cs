using Aw.Sindri.Core.Workflow.Engine.Store;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// What every <see cref="IWorkflowStore"/> honours (spec §5.3, §10): the in-memory fake (P3 unit (e)) and
/// SqliteWorkflowStore (P4) bind it alike, the fake in the unit lane and SQLite in the integration lane. Its tests
/// land with unit (e): a recorded key replays without writing, a moved version is SND-WF-002 and writes nothing,
/// and folding a row's events rebuilds it ('Folding_the_events_rebuilds_the_row', on every binding).
/// </summary>
public abstract class WorkflowStoreContract
{
    /// <summary>A store over a fresh, empty memory or file.</summary>
    /// <param name="clock">The only clock.</param>
    /// <returns>The store under test.</returns>
    protected abstract ValueTask<IWorkflowStore> Create(TimeProvider clock);
}
