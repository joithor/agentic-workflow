using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// Keeps the 'recipe-stats' view honest (API spec §3.8, P12): the view is a cache the stats reads fill, so this fold only empties it, when a run starts
/// ('workflow.started') or ends (a 'workflow.transition' whose post-state delta carries the terminal outcome). An end row names no recipe, so it empties
/// every recipe's entries; a run is rare next to a read.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class RecipeStatsProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.RecipeStats];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.WorkflowStarted, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.WorkflowTransition, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return RunStartsOrEnds(recorded)
            ? await rows.Clear(ViewNames.RecipeStats, cancellationToken).ConfigureAwait(false)
            : Unit.Value;
    }

    private bool RunStartsOrEnds(LedgerEvent recorded)
    {
        if (string.Equals(recorded.Envelope.Type, EventTypes.WorkflowStarted, StringComparison.Ordinal))
        {
            return true;
        }

        var payload = events.Payload<WorkflowEventPayload>(recorded);
        return payload.IsSuccess && payload.Value.Detail?.Delta.Outcome is not null;   // a row that doesn't read as a workflow payload ends nothing the stats count either
    }
}
