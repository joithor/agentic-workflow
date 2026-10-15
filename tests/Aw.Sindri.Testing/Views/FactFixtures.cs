using System.Text.Json;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;

namespace Aw.Sindri.Testing.Views;

/// <summary>
/// The bodies behind the fact rows (the facts route, API spec section 3.8; screens spec section 4.2): a scope-map table with
/// its graph and items columns, a packet with its PKT-* results, a payload tree, text, and a truncated payload. A body is
/// an object for scope-map, packet and payload (also when truncated) and a string for text. Each is built from small shapes
/// and serialized, so no JSON is written by hand.
/// </summary>
public static class FactFixtures
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private sealed record Surface(string Name, string Graph, string[] Files, string[] Items);

    private sealed record ScopeMap(string Brief, Surface[] Surfaces);

    private sealed record Task(string Id, string Title, int Acceptance, string[] Writes);

    private sealed record Stream(string Id, string Label, Task[] Tasks);

    private sealed record PacketCheck(string Id, string Result, string? Detail, string[] Tasks);

    private sealed record Packet(string Id, bool Frozen, Stream[] Workstreams, PacketCheck[] Checks);

    private sealed record Incident(string Id, string Title, string Urgency, string Service, string Status, string CreatedAt);

    private sealed record Alert(string Source, Incident Incident, string[] Tags);

    private sealed record Event(int N, string Kind);

    private sealed record EventLog(string Source, Event[] Events);

    private static JsonElement Body<T>(T value) => JsonSerializer.SerializeToElement(value, _options);

    /// <summary>The body behind one fact row, by scenario: scope-map, packet, text, payload or truncated.</summary>
    /// <param name="scenario">The scenario name.</param>
    /// <returns>The fact view.</returns>
    public static FactView Fact(string scenario) => scenario switch
    {
        "scope-map" => new FactView("scope", FactType.ScopeMap, Body(new ScopeMap("PRD-sync.md",
        [
            new Surface("sync store", "mapped", ["src/sync/store.ts", "src/sync/outbox.ts"], ["ITM-01JC1", "ITM-01JC2"]),
            new Surface("status pill", "mapped", ["ui/sync-pill.tsx"], ["ITM-01JC3"]),
            new Surface("conflict dialog", "new", ["ui/conflict-dialog.tsx"], ["ITM-01JC5"]),
            new Surface("background sync (iOS)", "unmapped", ["ios/BackgroundSync.swift"], []),
        ])), false),
        "packet" => new FactView("packet", FactType.Packet, Body(new Packet("3f1c09", false,
        [
            new Stream("ws-1", "UI", [new Task("t1", "Sync status pill", 3, ["ui/sync-pill.tsx"]), new Task("t2", "Conflict resolver dialog", 4, ["ui/conflict-dialog.tsx"])]),
            new Stream("ws-2", "Store", [new Task("t4", "PUT /sync/changes", 5, ["src/sync/queue.ts"]), new Task("t7", "Outbox queue", 3, ["src/sync/queue.ts"])]),
        ],
        [
            new PacketCheck("PKT-SCHEMA-001", "pass", null, []),
            new PacketCheck("PKT-ACCEPT-001", "pass", null, []),
            new PacketCheck("PKT-SCOPE-002", "fail", "t4 and t7 both write src/sync/queue.ts", ["t4", "t7"]),
        ])), false),
        "text" => new FactView("severity", FactType.Text, Body("sev2: the outbox dispatcher retries without backoff"), false),
        "payload" => new FactView("alert", FactType.Payload, Body(new Alert("pagerduty",
            new Incident("P-2211", "Outbox retry storm", "high", "outbox-dispatcher", "triggered", "2026-10-12T11:02:00Z"), ["prod", "outbox"])), false),
        "truncated" => new FactView("alert", FactType.Payload, Body(new EventLog("pagerduty", [new Event(1, "triggered"), new Event(2, "acknowledged")])), true),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such fact scenario"),
    };
}
