using System.Text;
using System.Text.Json;
using Aw.Kernel.Events;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>Builds ledger entries: a fresh correlation per command, the scope's actor, tenant and ring, the payload as AwJson.</summary>
/// <param name="scope">Who the process acts as.</param>
/// <param name="clock">The clock.</param>
/// <param name="ids">The id source.</param>
/// <param name="json">The package's AwJson options (the payload records are in its context).</param>
public sealed class EventFactory(EventScope scope, TimeProvider clock, IIdGenerator ids, JsonSerializerOptions json)
{
    /// <summary>An entry of <paramref name="type"/> with <paramref name="payload"/> and the idempotency <paramref name="key"/>.</summary>
    /// <param name="type">The event type, for example 'process.started'.</param>
    /// <param name="payload">The payload record.</param>
    /// <param name="key">The idempotency key: what identifies the fact, so recording it twice is a no-op.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>The entry.</returns>
    public LedgerEntry Entry<TPayload>(string type, TPayload payload, string key)
    {
        var context = EventContext.Create(scope.Actor, scope.Tenant, scope.Ring, ids.NewId()).Value;
        var text = Encoding.UTF8.GetString(AwJson.Serialize(payload, json));
        return new LedgerEntry(context.Envelope(type, text, clock, ids), key, Item: null);
    }
}
