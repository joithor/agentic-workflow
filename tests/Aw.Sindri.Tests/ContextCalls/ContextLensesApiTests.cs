using System.Net;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The app's two reads (lenses and filemap), the scoping run's proposals and MCP's context_lenses (context spec §8.5, §8.9; README X63) on the fixture map.</summary>
public sealed class ContextLensesApiTests : IDisposable
{
    private const string Anchor = "feature:tabs.tab-manager";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private async Task<GoldenHost> Host(bool sessions = true)
    {
        var host = await ContextHosts.Approved(_repo, sessions, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        return host;
    }

    private static (string Name, string Value)[] Terminal(GoldenHost host) => Human(host.Token.Value);

    private static (string Name, string Value)[] App(GoldenHost host) => [.. Human(host.Token.Value, client: "ui").Where(h => h.Name != SindriHeaders.Terminal)];

    private static (string Name, string Value)[] Session(string repo = FixtureMap.Repo) => [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, repo)];

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_show_the_three_bands_a_session_reads_its_scope_and_an_unknown_anchor_is_003_and_no_draft_is_044()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var lenses = await host.Http.Ask(Get("/sindri/context/lenses?anchor=" + Uri.EscapeDataString(Anchor), Terminal(host)));
        var session = await host.Http.Ask(Get("/sindri/context/lenses?anchor=" + Uri.EscapeDataString(Anchor), Session()));
        var unknown = await host.Http.Ask(Get("/sindri/context/lenses?anchor=no-such-node", Terminal(host)));
        var draft = await host.Http.Ask(Get("/sindri/context/lenses?anchor=" + Uri.EscapeDataString(Anchor) + "&draft=true", Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, lenses.Status);
        var view = Json(lenses.Body);
        Assert.Equal(Anchor, view.GetProperty("anchor").GetString());
        Assert.All(["intent", "product", "code", "coverage", "conflicts"], name => Assert.True(view.TryGetProperty(name, out _), name));
        Assert.Equal(HttpStatusCode.OK, session.Status);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (unknown.Status, unknown.Code));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-044"), (draft.Status, draft.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Filemap_lists_direct_children_with_rollups_and_a_repo_outside_the_profile_or_a_session_scope_gives_the_same_045()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var root = await host.Http.Ask(Get($"/sindri/context/filemap?repo={FixtureMap.Repo}&path=src/", Terminal(host)));
        var unmapped = await host.Http.Ask(Get($"/sindri/context/filemap?repo={FixtureMap.Repo}&path=src/&unmapped=true", Terminal(host)));
        var nope = await host.Http.Ask(Get("/sindri/context/filemap?repo=nope", Terminal(host)));
        var outside = await host.Http.Ask(Get($"/sindri/context/filemap?repo={FixtureMap.Repo}", Session("other")));
        var draft = await host.Http.Ask(Get($"/sindri/context/filemap?repo={FixtureMap.Repo}&draft=true", Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, root.Status);
        var view = Json(root.Body);
        Assert.Equal("src/", view.GetProperty("path").GetString());
        Assert.Contains(view.GetProperty("entries").EnumerateArray(), e => e.GetProperty("name").GetString() == "layout" && e.GetProperty("directory").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, unmapped.Status);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-045"), (nope.Status, nope.Code));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-045"), (outside.Status, outside.Code));
        Assert.Equal(Json(nope.Body).GetProperty("status").GetInt32(), Json(outside.Body).GetProperty("status").GetInt32());
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-044"), (draft.Status, draft.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_lenses_over_mcp_returns_the_lenses_as_structured_content_and_a_session_off_is_020()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: ct);
        var result = await mcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}"}""", ct);
        var bad = await mcp.Call("context_lenses", """{"anchor":"x","draft":true}""", ct);

        Assert.Contains("context_lenses", tools.Select(t => t.Name), StringComparer.Ordinal);
        Assert.True(tools.Single(t => t.Name == "context_lenses").ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.NotEqual(true, result.IsError);
        Assert.Equal(Anchor, result.StructuredContent!.Value.GetProperty("anchor").GetString());
        Assert.Equal(true, bad.IsError);   // no draft argument over MCP
        await using var off = await OffHost();
        await using var offMcp = await McpSession.Open(off, FixtureMap.Repo, ct);
        var refused = await offMcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}"}""", ct);
        Assert.Equal(true, refused.IsError);
        Assert.Contains("SND-CONTEXT-020", refused.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
    }

    private async Task<GoldenHost> OffHost() => await Host(sessions: false);

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_scoping_run_proposes_into_a_draft_idempotently_never_again_after_a_reject_and_never_into_current()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        var body = $$"""{"run":"scope-1","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"Tab manager","key":"{{Anchor}}"},{"name":"Zoom","kind":"feature","evidence":[]},{"name":"Something vague"}]}""";

        var first = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var second = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var session = await host.Http.Ask(Post("/sindri/context/map/proposals", body, Session()));
        var view = await host.Http.Ask(Get("/sindri/context/map/proposals?run=scope-1", App(host)));
        var reject = await host.Http.Ask(Post("/sindri/context/map/review", """{"verb":"reject","node":"feature:zoom"}""", App(host)));
        var third = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var status = await host.Http.Ask(Get("/sindri/context/map", Terminal(host)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapProposed, ct);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(1, Json(first.Body).GetProperty("result").GetProperty("added").GetInt32());
        Assert.Equal(0, Json(second.Body).GetProperty("result").GetProperty("added").GetInt32());
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (session.Status, session.Code));
        var statuses = Json(view.Body).GetProperty("surfaces").EnumerateArray().Select(s => s.GetProperty("status").GetString()).ToList();
        Assert.Equal(["mapped", "new", "unmapped"], statuses.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(HttpStatusCode.OK, reject.Status);
        Assert.Equal(0, Json(third.Body).GetProperty("result").GetProperty("added").GetInt32());
        Assert.Equal(1, Json(third.Body).GetProperty("result").GetProperty("rejected").GetInt32());
        Assert.Contains("\"status\":\"current\"", status.Body, StringComparison.Ordinal);
        Assert.Contains("scope-1", recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
    }
}
