using System.Net;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>GET and POST /sindri/context/subgraph (scoping spec C14; API lane conditions 2 to 4): one handler, one session filter, one set of limits.</summary>
public sealed class ContextSubgraphApiTests : IDisposable
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string Other = "feature:layout.split-pane";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private async Task<GoldenHost> Host()
    {
        var host = await ContextHosts.Approved(_repo, sessions: true, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        return host;
    }

    private static (string Name, string Value)[] Terminal(GoldenHost host) => Human(host.Token.Value);

    private static string Nodes(IEnumerable<string> keys) => string.Join('&', keys.Select(k => "nodes=" + Uri.EscapeDataString(k)));

    private static string Body(IEnumerable<string> keys, int? depth = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal) { ["nodes"] = keys.ToArray(), ["depth"] = depth });

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    private static IEnumerable<string> Fake(int count) => Enumerable.Range(0, count).Select(i => "feature:nope." + i.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Subgraph_get_repeated_nodes_answers_and_names_what_is_missing()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var answer = await host.Http.Ask(Get("/sindri/context/subgraph?" + Nodes([Manager, Other, "feature:nope.x"]), Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var view = Json(answer.Body);
        Assert.Equal(["feature:nope.x"], view.GetProperty("missing").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        Assert.Equal(2, view.GetProperty("nodes").GetArrayLength());
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Subgraph_get_21_nodes_is_006_and_a_value_over_MaxEncodedText_is_006_and_none_is_006()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var many = await host.Http.Ask(Get("/sindri/context/subgraph?" + Nodes(Fake(21)), Terminal(host)));
        var long_ = await host.Http.Ask(Get("/sindri/context/subgraph?" + Nodes(["feature:" + new string('漢', 900)]), Terminal(host)));
        var none = await host.Http.Ask(Get("/sindri/context/subgraph", Terminal(host)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (many.Status, many.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (long_.Status, long_.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (none.Status, none.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Subgraph_post_1000_keys_answers_and_1001_is_006()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var ok = await host.Http.Ask(Post("/sindri/context/subgraph", Body(Fake(1000)), Terminal(host)));
        var over = await host.Http.Ask(Post("/sindri/context/subgraph", Body(Fake(1001)), Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal(1000, Json(ok.Body).GetProperty("missing").GetArrayLength());
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (over.Status, over.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Subgraph_get_and_post_for_the_same_keys_answer_byte_identical_bodies_also_for_a_session()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        var keys = new[] { Manager, Other };
        (string Name, string Value)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

        var get = await host.Http.Ask(Get("/sindri/context/subgraph?" + Nodes(keys) + "&depth=2", Terminal(host)));
        var post = await host.Http.Ask(Post("/sindri/context/subgraph", Body(keys, depth: 2), Terminal(host)));
        var otherGet = await host.Http.Ask(Get("/sindri/context/subgraph?" + Nodes(keys) + "&depth=2", session));
        var otherPost = await host.Http.Ask(Post("/sindri/context/subgraph", Body(keys, depth: 2), session));

        Assert.Equal(get.Body, post.Body);
        Assert.Equal(otherGet.Body, otherPost.Body);
        Assert.Equal(2, Json(otherPost.Body).GetProperty("nodes").GetArrayLength());
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Subgraph_post_from_foreign_origin_is_403_SND_API_008()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var refused = await host.Http.Ask(Post("/sindri/context/subgraph", Body([Manager]), [.. Terminal(host), ("Origin", "http://localhost:3000")]));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-API-008"), (refused.Status, refused.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Subgraph_post_honours_X_Sindri_After()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var malformed = await host.Http.Ask(Post("/sindri/context/subgraph", Body([Manager]), [.. Terminal(host), (SindriHeaders.After, "soon")]));

        Assert.Equal(HttpStatusCode.BadRequest, malformed.Status);
        Assert.Contains("X-Sindri-After", malformed.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Subgraph_post_with_an_empty_body_list_is_006()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var none = await host.Http.Ask(Post("/sindri/context/subgraph", Body([]), Terminal(host)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (none.Status, none.Code));
    }
}
