namespace Aw.Sindri.Tests.BridgeEvents;

/// <summary>
/// Recipes made of event waits only, for the bridge's tests: 'workflow.relay' waits for the router ('item.routed', route
/// item), then for its claim ('claim.created', route causation); 'workflow.twice' waits for 'item.routed' twice, so a
/// delivery applied twice would show.
/// </summary>
internal static class BridgeRecipes
{
    public static readonly string Relay = string.Join('\n',
    [
        "schema: 1",
        "id: workflow.relay",
        "version: 1",
        "inputs: [fact:spec]",
        "initial: routed",
        "states:",
        "  routed:  { kind: wait, waitFor: { event: item.routed }, timeout: { after: 7d, then: escalate },",
        "             next: \"Waiting for the router to route {item}.\" }",
        "  claimed: { kind: wait, waitFor: { event: claim.created }, timeout: { after: 7d, then: escalate },",
        "             next: \"Waiting for the claim on {item}.\" }",
        "  done:    { kind: terminal, outcome: resolved }",
        "  dropped: { kind: terminal, outcome: unresolved }",
        "transitions:",
        "  - { from: routed,  to: claimed, on: wait.satisfied, when: { outcome: startable } }",
        "  - { from: routed,  to: dropped, on: wait.satisfied, when: { outcome: notStartable } }",
        "  - { from: claimed, to: done,    on: wait.satisfied, when: { outcome: created } }",
        "  - { from: claimed, to: dropped, on: wait.satisfied, when: { outcome: claimExists } }",
    ]);

    public static readonly string Twice = string.Join('\n',
    [
        "schema: 1",
        "id: workflow.twice",
        "version: 1",
        "inputs: [fact:spec]",
        "initial: first",
        "states:",
        "  first:   { kind: wait, waitFor: { event: item.routed }, timeout: { after: 7d, then: escalate },",
        "             next: \"Waiting for the first routing of {item}.\" }",
        "  second:  { kind: wait, waitFor: { event: item.routed }, timeout: { after: 7d, then: escalate },",
        "             next: \"Waiting for the second routing of {item}.\" }",
        "  done:    { kind: terminal, outcome: resolved }",
        "  dropped: { kind: terminal, outcome: unresolved }",
        "transitions:",
        "  - { from: first,  to: second,  on: wait.satisfied }",
        "  - { from: second, to: done,    on: wait.satisfied, when: { outcome: startable } }",
        "  - { from: second, to: dropped, on: wait.satisfied, when: { outcome: notStartable } }",
    ]);

    /// <summary>Writes both recipes under <paramref name="home"/>'s recipes directory, where the stub approvals read them.</summary>
    public static void WriteTo(string home)
    {
        var recipes = Directory.CreateDirectory(Path.Combine(home, "recipes")).FullName;
        File.WriteAllText(Path.Combine(recipes, "workflow.relay.yaml"), Relay);
        File.WriteAllText(Path.Combine(recipes, "workflow.twice.yaml"), Twice);
    }
}
