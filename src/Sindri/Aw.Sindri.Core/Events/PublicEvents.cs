using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Events;

/// <summary>Maps a ledger row to the <see cref="PublicEvent"/> the stream and ledger reads send (API spec §3.6, §5.3): the one place that decides what a caller sees of a row.</summary>
public static class PublicEvents
{
    private static readonly FrozenDictionary<string, EventCatalogEntry> _catalog = EventCatalog.All.ToFrozenDictionary(static e => e.Type, StringComparer.Ordinal);

    /// <summary>
    /// The public form of <paramref name="row"/>: null when its type isn't public and <paramref name="includeNonPublic"/> is
    /// false; else the envelope with the catalog's public payload (a record that leaves out what a caller may not see, such
    /// as an item's body, by construction; the whole payload for a non-public type), every string scrubbed and escaped.
    /// A payload that doesn't parse is sent as JSON null, so the stream never stalls on one row.
    /// </summary>
    /// <param name="row">The ledger row.</param>
    /// <param name="options">The options that know each public payload type.</param>
    /// <param name="scrubber">Masks secrets in every string of the payload.</param>
    /// <param name="includeNonPublic">True for a human's ledger read.</param>
    /// <returns>The event, or null.</returns>
    public static Result<PublicEvent?> For(LedgerEvent row, JsonSerializerOptions options, ITextScrubber scrubber, bool includeNonPublic)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scrubber);
        var e = row.Envelope;
        _ = _catalog.TryGetValue(e.Type, out var entry);
        var shown = entry is { Public: true, PublicPayload: not null };
        if (!shown && !includeNonPublic)
        {
            return (PublicEvent?)null;
        }

        return new PublicEvent(row.Sequence, EventId.From(e.Id), e.Type, e.SchemaVersion, e.OccurredAt, e.Actor, e.Tenant, e.Ring,
            ItemId.TryFrom(row.Item ?? string.Empty, out var item) ? item : null, CorrelationId.From(e.CorrelationId),
            e.CausationId is { } cause ? EventId.From(cause) : null, TraceId.TryFrom(e.TraceId ?? string.Empty, out var trace) ? trace : null,
            Payload(e.Payload, entry is { Public: true } ? entry.PublicPayload : null, options, scrubber));
    }

    private static JsonElement NullElement { get; } = JsonDocument.Parse("null").RootElement.Clone();

    private static JsonElement Payload(string text, Type? publicType, JsonSerializerOptions options, ITextScrubber scrubber)
    {
        try
        {
            var node = publicType is null
                ? JsonNode.Parse(text)
                : JsonSerializer.SerializeToNode(JsonSerializer.Deserialize(text, options.GetTypeInfo(publicType)), options.GetTypeInfo(publicType));
            using var element = JsonDocument.Parse(Clean(node, scrubber)?.ToJsonString() ?? "null");
            return element.RootElement.Clone();
        }
        catch (JsonException)
        {
            return NullElement;
        }
    }

    private static JsonNode? Clean(JsonNode? node, ITextScrubber scrubber)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (name, value) in o.ToArray())
                {
                    o[name] = Clean(value, scrubber);
                }

                return o;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    a[i] = Clean(a[i], scrubber);
                }

                return a;
            case JsonValue v when v.TryGetValue<string>(out var s):
                return JsonValue.Create(SafeText.Escape(scrubber.Scrub(s)));
            default:
                return node;
        }
    }
}
