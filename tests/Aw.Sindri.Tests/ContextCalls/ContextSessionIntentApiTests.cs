using System.Net;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Owner decision 2026-10-15: B. A session (HTTP and MCP) sees the records and decisions linked to its repo's nodes, fenced, and nothing linked only to other nodes (context spec §3).</summary>
public sealed class ContextSessionIntentApiTests : IDisposable
{
    private const string OutRecord = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000b02";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static (string Name, string Value)[] Session() => [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    private async Task<GoldenHost> Host()
    {
        var host = await ContextHosts.Approved(_repo, sessions: true, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        var file = Path.Combine(host.Home, "intent-map.yaml");
        var inDecision = DecisionRow.KeyFor(RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000a01"), "keep the pane in view").Value;
        var outDecision = DecisionRow.KeyFor(RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000b01"), "other quote").Value;
        await File.WriteAllTextAsync(file, string.Join('\n',
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: \"In scope\"}",
            "  - {key: \"feature:out\", label: \"Out of scope\"}",
            "  - {key: \"code:src/in.ts\", label: \"in.ts\", provenance: deterministic}",
            "  - {key: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\", label: \"Mentions in\"}",
            $"  - {{key: \"{OutRecord}\", label: \"OUTONLY-TITLE-ZEBRA\"}}",
            "edges:",
            "  - {from: \"code:src/in.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in.ts\"}]}",
            "  - {from: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\", to: \"feature:in\", type: mentions}",
            $"  - {{from: \"{OutRecord}\", to: \"feature:out\", type: mentions}}",
            $"  - {{from: \"{inDecision}\", to: \"feature:in\", type: decides}}",
            $"  - {{from: \"{outDecision}\", to: \"feature:out\", type: decides}}",
            "decisions:",
            "  - {record: \"linear:issue/0e5a1b00-0000-4000-8000-000000000a01\", quote: \"keep the pane in view\", wording: \"Keep the pane in view.\", at: 2026-07-15T10:00:00Z}",
            "  - {record: \"linear:issue/0e5a1b00-0000-4000-8000-000000000b01\", quote: \"other quote\", wording: \"OUTONLY-TITLE-ZEBRA decision.\", at: 2026-07-15T10:00:00Z}") + "\n", TestContext.Current.CancellationToken);
        await ContextMapApiTests.Import(host, file);
        return host;
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_session_over_http_sees_the_intent_linked_to_its_nodes_and_003_for_a_record_linked_only_elsewhere()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var lenses = await host.Http.Ask(Get("/sindri/context/lenses?anchor=feature%3Ain", Session()));
        var query = await host.Http.Ask(Get("/sindri/context/query?node=feature%3Ain", Session()));
        var outside = await host.Http.Ask(Get("/sindri/context/node?key=" + Uri.EscapeDataString(OutRecord), Session()));

        Assert.Equal(HttpStatusCode.OK, lenses.Status);
        var intent = Json(lenses.Body).GetProperty("bands").GetProperty("intent");
        Assert.Equal((1, 1), (intent.GetProperty("counts").GetProperty("records").GetInt32(), intent.GetProperty("counts").GetProperty("decisions").GetInt32()));
        Assert.StartsWith("<untrusted source=\"map\">", intent.GetProperty("decisions")[0].GetProperty("quote").GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ZEBRA", lenses.Body + query.Body, StringComparison.Ordinal);
        Assert.Contains("\"decisions\"", query.Body, StringComparison.Ordinal);
        Assert.Contains("keep the pane in view", query.Body, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (outside.Status, outside.Code));
        Assert.DoesNotContain("ZEBRA", outside.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_lenses_over_mcp_carries_the_linked_intent_and_not_the_other()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var result = await mcp.Call("context_lenses", """{"anchor":"feature:in"}""", ct);
        var outside = await mcp.Call("context_lenses", $$"""{"anchor":"{{OutRecord}}"}""", ct);

        Assert.NotEqual(true, result.IsError);
        var counts = result.StructuredContent!.Value.GetProperty("bands").GetProperty("intent").GetProperty("counts");
        Assert.Equal((1, 1), (counts.GetProperty("records").GetInt32(), counts.GetProperty("decisions").GetInt32()));
        Assert.StartsWith("<untrusted source=\"map\">", result.StructuredContent!.Value.GetProperty("bands").GetProperty("intent").GetProperty("decisions")[0].GetProperty("quote").GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ZEBRA", result.StructuredContent.Value.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(true, outside.IsError);
        Assert.Contains("SND-CONTEXT-003", outside.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("ZEBRA", outside.StructuredContent.Value.GetRawText(), StringComparison.Ordinal);
    }
}
