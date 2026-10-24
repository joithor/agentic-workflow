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

/// <summary>QUERY /sindri/context/lenses, /code and /history: the anchors in a JSON body, for a list too long for the request line (Kestrel's 8 KB, a 414). One service and one session filter with the GET, so the bodies match.</summary>
public sealed class ContextBodyQueryApiTests : IDisposable
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string Other = "feature:layout.split-pane";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    public static TheoryData<string> Routes => ["lenses", "code", "history"];

    private async Task<GoldenHost> Host()
    {
        var host = await ContextHosts.Approved(_repo, sessions: true, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        return host;
    }

    private static (string Name, string Value)[] Terminal(GoldenHost host) => Human(host.Token.Value);

    private static (string Name, string Value)[] Session() => [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

    private static string Body(IEnumerable<string> anchors, int? depth = null, string? mapVersion = null, bool? draft = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal) { ["anchors"] = anchors.ToArray(), ["depth"] = depth, ["mapVersion"] = mapVersion, ["draft"] = draft }
            .Where(static pair => pair.Value is not null).ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));   // an unset field is omitted: 'draft' is a bool, and AwJson refuses a null for it

    private static string Gets(IEnumerable<string> anchors) => string.Join('&', anchors.Select(a => "anchor=" + Uri.EscapeDataString(a)));

    private static IEnumerable<string> Fake(int count) => Enumerable.Range(0, count).Select(i => "feature:nope-" + new string('x', 20) + "." + i.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    [Theory(Timeout = 120_000), Trait("Lane", "integration"), MemberData(nameof(Routes))]
    public async Task Query_for_250_anchors_answers_200_where_the_get_is_a_414(string route)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        var anchors = new[] { Manager, Other }.Concat(Fake(248)).ToArray();

        var get = await host.Http.Ask(Get($"/sindri/context/{route}?" + Gets(anchors), Terminal(host)));
        var query = await host.Http.Ask(Query($"/sindri/context/{route}", Body(anchors), Terminal(host)));

        Assert.Equal(HttpStatusCode.RequestUriTooLong, get.Status);
        Assert.Equal(HttpStatusCode.OK, query.Status);
        Assert.Equal(248, Json(query.Body).GetProperty("missingAnchors").GetArrayLength());
    }

    [Theory(Timeout = 120_000), Trait("Lane", "integration"), MemberData(nameof(Routes))]
    public async Task Query_and_get_for_a_small_set_answer_the_same_body_for_a_human_and_a_session(string route)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        var anchors = new[] { Manager, Other };

        foreach (var headers in new[] { Terminal(host), Session() })
        {
            var get = await host.Http.Ask(Get($"/sindri/context/{route}?" + Gets(anchors), headers));
            var query = await host.Http.Ask(Query($"/sindri/context/{route}", Body(anchors), headers));

            Assert.Equal(HttpStatusCode.OK, get.Status);
            Assert.Equal(get.Body, query.Body);
        }
    }

    [Theory(Timeout = 120_000), Trait("Lane", "integration"), MemberData(nameof(Routes))]
    public async Task Query_with_no_anchor_is_006_and_a_draft_with_a_map_version_is_006(string route)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var none = await host.Http.Ask(Query($"/sindri/context/{route}", Body([]), Terminal(host)));
        var both = await host.Http.Ask(Query($"/sindri/context/{route}", Body([Manager], mapVersion: "v1", draft: true), Terminal(host)));
        var noDraft = await host.Http.Ask(Query($"/sindri/context/{route}", Body([Manager], draft: true), Terminal(host)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (none.Status, none.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (both.Status, both.Code));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-044"), (noDraft.Status, noDraft.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_query_takes_depth_and_hops_and_one_distinct_anchor_stays_schema_1()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var one = await host.Http.Ask(Query("/sindri/context/lenses", """{"anchors":["feature:tabs.tab-manager","feature:tabs.tab-manager"],"depth":1,"hops":"state,flow"}""", Terminal(host)));
        var bogus = await host.Http.Ask(Query("/sindri/context/lenses", """{"anchors":["feature:tabs.tab-manager"],"hops":"bogus"}""", Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, one.Status);
        Assert.Equal((1, 1), (Json(one.Body).GetProperty("schemaVersion").GetInt32(), Json(one.Body).GetProperty("depth").GetInt32()));
        Assert.Equal("SND-CONTEXT-140", bogus.Code);
    }

    [Theory(Timeout = 120_000), Trait("Lane", "integration"), MemberData(nameof(Routes))]
    public async Task No_anchor_is_400_006_naming_the_call_on_the_get_and_the_query(string route)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var get = await host.Http.Ask(Get($"/sindri/context/{route}", Terminal(host)));
        var query = await host.Http.Ask(Query($"/sindri/context/{route}", Body([]), Terminal(host)));

        Assert.All([get, query], answer =>
        {
            Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (answer.Status, answer.Code));
            Assert.Contains($"a {route} call takes at least 1 anchor, not 0", answer.Body, StringComparison.Ordinal);
        });
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Query_with_explicit_nulls_for_the_optional_fields_answers_the_same_as_with_them_omitted()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        (string Route, string WithNulls, string Omitted)[] cases =
        [
            ("lenses", """{"anchors":["feature:tabs.tab-manager","feature:layout.split-pane"],"depth":null,"draft":false,"mapVersion":null,"hops":null}""", """{"anchors":["feature:tabs.tab-manager","feature:layout.split-pane"]}"""),
            ("code", """{"anchors":["feature:tabs.tab-manager","feature:layout.split-pane"],"draft":false,"mapVersion":null}""", """{"anchors":["feature:tabs.tab-manager","feature:layout.split-pane"]}"""),
        ];

        foreach (var (route, withNulls, omitted) in cases)
        {
            var nulls = await host.Http.Ask(Query($"/sindri/context/{route}", withNulls, Terminal(host)));
            var plain = await host.Http.Ask(Query($"/sindri/context/{route}", omitted, Terminal(host)));

            Assert.Equal(HttpStatusCode.OK, nulls.Status);
            Assert.Equal(HttpStatusCode.OK, plain.Status);
            Assert.Equal(plain.Body, nulls.Body);
        }
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task History_query_takes_since_and_before()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        const string Url = "/sindri/context/history";

        var get = await host.Http.Ask(Get(Url + "?anchor=" + Uri.EscapeDataString(Other) + "&since=2026-07-20T00:00:00Z", Terminal(host)));
        var query = await host.Http.Ask(Query(Url, $$"""{"anchors":["{{Other}}"],"since":"2026-07-20T00:00:00Z"}""", Terminal(host)));
        var bad = await host.Http.Ask(Query(Url, $$"""{"anchors":["{{Other}}"],"since":"90d"}""", Terminal(host)));

        Assert.Equal(get.Body, query.Body);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (bad.Status, bad.Code));
    }

    [Theory(Timeout = 120_000), Trait("Lane", "integration"), MemberData(nameof(Routes))]
    public async Task Query_from_a_foreign_origin_is_403_and_post_is_405(string route)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var origin = await host.Http.Ask(Query($"/sindri/context/{route}", Body([Manager]), [.. Terminal(host), ("Origin", "http://localhost:3000")]));
        var post = await host.Http.Ask(Post($"/sindri/context/{route}", Body([Manager]), Terminal(host)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-API-008"), (origin.Status, origin.Code));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.Status);
    }
}
