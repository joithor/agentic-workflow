using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Items;

/// <summary>The one line an item's timeline shows for an event (API spec §3.1).</summary>
public static class TimelineLine
{
    /// <summary>The line for <paramref name="recorded"/>: 'Created: &lt;title&gt;', 'new → done', 'Linked to &lt;record&gt; (&lt;alias&gt;)', or the event type.</summary>
    /// <param name="recorded">The ledger event.</param>
    /// <param name="json">The options its payload reads with.</param>
    /// <returns>The line, before scrubbing.</returns>
    public static string For(LedgerEvent recorded, JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        var bytes = Encoding.UTF8.GetBytes(recorded.Envelope.Payload);
        var type = recorded.Envelope.Type;
        var line = type switch
        {
            EventTypes.ItemCreated => AwJson.Deserialize<ItemCreated>(bytes, json).Map(static created => $"Created: {created.Title}"),
            EventTypes.ItemStateChanged => AwJson.Deserialize<ItemStateChanged>(bytes, json).Map(static changed => $"{ItemStateNames.Of(changed.From)} → {ItemStateNames.Of(changed.To)}"),
            EventTypes.ItemLinked => AwJson.Deserialize<ItemLinked>(bytes, json).Map(static linked => linked.Alias is { } alias ? $"Linked to {linked.Record.Value} ({alias.Value})" : $"Linked to {linked.Record.Value}"),
            _ => type,
        };
        return line.IsError ? type : line.Value;
    }
}
