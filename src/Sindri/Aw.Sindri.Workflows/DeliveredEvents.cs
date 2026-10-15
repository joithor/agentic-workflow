using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// A registered ledger event as the trigger the bridge signals (engine spec §2, §5.4): 'wait.satisfied' from an
/// <see cref="EventWaitSource"/> keyed by the event's ledger sequence, with the payload's outcome and the facts the
/// descriptor declares. A payload that doesn't read, or an outcome the descriptor doesn't declare, makes no trigger.
/// </summary>
internal static class DeliveredEvents
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    /// <summary>The trigger <paramref name="recorded"/> makes, or why it makes none.</summary>
    /// <param name="descriptor">The event's registration.</param>
    /// <param name="recorded">The ledger event.</param>
    /// <param name="actor">The delivering service.</param>
    /// <returns>The trigger, or null and the problem.</returns>
    public static (WaitSatisfied? Trigger, string? Problem) Trigger(EventDescriptor descriptor, LedgerEvent recorded, string actor)
    {
        var read = AwJson.Deserialize<RegisteredEventPayload>(Encoding.UTF8.GetBytes(recorded.Envelope.Payload), _json);
        if (read.IsError)
        {
            return (null, "its payload doesn't read: " + read.Error.Message);
        }

        OutcomeName? outcome = null;
        if (read.Value.Outcome is { } named)
        {
            if (!descriptor.Outcomes.Any(o => string.Equals(o.Value, named, StringComparison.Ordinal)))
            {
                return (null, $"its outcome '{named}' isn't one of {string.Join(", ", descriptor.Outcomes.Select(static o => o.Value))}");
            }

            outcome = OutcomeName.From(named);
        }

        var facts = read.Value.Facts ?? new Dictionary<string, FactValue>(StringComparer.Ordinal);
        var outputs = descriptor.Facts.Where(k => facts.ContainsKey(k.Value)).ToDictionary(static k => k, k => facts[k.Value]);
        return (new WaitSatisfied(actor, new EventWaitSource(descriptor.Kind, recorded.Sequence), outcome, outputs) { OccurredAt = recorded.Envelope.OccurredAt }, null);
    }
}
