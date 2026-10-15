using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The S-4 context verbs over a scripted API: what they send, what they print and the exit codes (0 ok, 1 attention, 2 tool error, 75 the heavy lock).</summary>
public sealed class ContextReviewVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (HttpStatusCode, string) Ok(string body) => (HttpStatusCode.OK, body);

    private static (HttpStatusCode, string) Problem(HttpStatusCode status, string code, string detail) =>
        (status, string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"type":"x","title":"{{code}}","status":{{(int)status}},"detail":"{{detail}}"}"""));

    private async Task<int> Run(ScriptedApi api, bool interactive, string typed, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        var verbs = new ContextMapVerbs(client, new Output(_out, _error, SliceHarness.Json), interactive, new StringReader(typed));
        return await verbs.Run(args, Token);
    }

    private async Task<int> Lens(ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        return await new ContextLensVerbs(client, new Output(_out, _error, SliceHarness.Json)).Run(args, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Promote_shows_the_draft_asks_for_the_challenge_and_sends_the_typed_answer()
    {
        var api = new ScriptedApi(
            Ok("""{"draft":"v14@0a1b2c3","promoted":null,"sequence":null,"challenge":{"challenge":"CHL-00000000000000000000000000000001","action":"promote map v14","expiresAt":"2026-10-15T00:05:00Z"}}"""),
            Ok("""{"draft":"v14@0a1b2c3","promoted":{"version":{"number":14,"repo":"aw-split-panes","commit":null,"keyVersion":1,"created":"2026-10-15T00:00:00Z","status":"current"},"retained":null},"sequence":7,"challenge":null}"""));

        var exit = await Run(api, interactive: true, "promote map v14\n", "promote");

        Assert.True(exit == 0, _error.ToString());
        Assert.Equal(2, api.Calls.Count);
        Assert.Contains("\"draft\":\"v14@0a1b2c3\"", api.Calls[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"typed\":\"promote map v14\"", api.Calls[1].Body, StringComparison.Ordinal);
        Assert.Contains("Type 'promote map v14' to confirm", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("Promoted v14@0a1b2c3 to current", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Promote_without_a_terminal_sends_nothing_after_the_preview_and_exits_1()
    {
        var api = new ScriptedApi(Ok("""{"draft":"v14@0a1b2c3","promoted":null,"sequence":null,"challenge":{"challenge":"CHL-00000000000000000000000000000001","action":"promote map v14","expiresAt":"2026-10-15T00:05:00Z"}}"""));

        var exit = await Run(api, interactive: false, string.Empty, "promote");

        Assert.True(exit == 1, _error.ToString());
        _ = Assert.Single(api.Calls);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Promote_with_undecided_items_is_SND_CONTEXT_046_exit_1()
    {
        var api = new ScriptedApi(Problem(HttpStatusCode.Conflict, "SND-CONTEXT-046", "draft v14@0a1b2c3 has 3 undecided review items"));

        var exit = await Run(api, interactive: true, string.Empty, "promote");

        Assert.Equal(1, exit);
        Assert.Contains("SND-CONTEXT-046 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_heavy_lock_exits_75_and_an_unreadable_repo_exits_2()
    {
        var busy = await Run(new ScriptedApi(Problem(HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-040", "the heavy-job lock stayed busy")), interactive: true, string.Empty, "review", "--accept-deterministic");
        var unreadable = await Run(new ScriptedApi(Problem(HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-048", "git can't read the repo")), interactive: true, string.Empty, "review", "--accept-deterministic");

        Assert.Equal((75, 2), (busy, unreadable));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_build_is_pending_API_P6_and_exits_2_like_a_pending_form()
    {
        var api = new ScriptedApi(Ok("{}"));

        var exit = await Run(api, interactive: true, string.Empty, "build");

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
        Assert.Contains("pending:API-P6", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_single_decision_is_sent_at_once_and_prints_the_rows_and_what_is_left()
    {
        var api = new ScriptedApi(Ok("""{"result":{"written":["ovr_0123456789abcdef"],"skipped":[],"undecided":4},"sequence":9,"challenge":null,"summary":null}"""));

        var exit = await Run(api, interactive: false, string.Empty, "review", "accept", "feature:tabs");

        Assert.Equal(0, exit);
        var (method, route, body) = Assert.Single(api.Calls);
        Assert.Equal(("POST", "/sindri/context/map/review"), (method, route));
        Assert.Contains("\"verb\":\"accept\"", body, StringComparison.Ordinal);
        Assert.Contains("\"node\":\"feature:tabs\"", body, StringComparison.Ordinal);
        Assert.Contains("1 row written, 4 undecided", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_loop_sends_the_bulk_accept_and_each_decision_and_skip_sends_nothing()
    {
        const string Queue = """{"draft":"v14@0a1b2c3","entries":[{"node":"surface:command/close-tab","link":null,"group":"deterministic","label":"Close tab","provenance":"deterministic","confidence":1,"impact":1,"files":[],"parent":null},{"node":"feature:layout","link":null,"group":"model","label":"Layout","provenance":"model","confidence":0.7,"impact":1,"files":[],"parent":null},{"node":"feature:zoom","link":null,"group":"model","label":"Zoom","provenance":"model","confidence":0.6,"impact":0,"files":[],"parent":null}],"decided":0}""";
        const string Done = """{"result":{"written":["ovr_0123456789abcdef"],"skipped":[],"undecided":1},"sequence":9,"challenge":null,"summary":null}""";
        var api = new ScriptedApi(Ok(Queue), Ok(Done), Ok(Done));

        var exit = await Run(api, interactive: true, "y\na\ns\n", "review");

        Assert.Equal(0, exit);
        Assert.Equal(3, api.Calls.Count);   // the queue, the bulk accept, the accept; the skip sends nothing
        Assert.Contains("\"verb\":\"accept-deterministic\"", api.Calls[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"node\":\"feature:layout\"", api.Calls[2].Body, StringComparison.Ordinal);
        Assert.Contains("skipped 1", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_protected_review_action_types_the_challenge_and_sends_it_again_with_the_answer()
    {
        var api = new ScriptedApi(
            Ok("""{"result":null,"sequence":null,"challenge":{"challenge":"CHL-00000000000000000000000000000002","action":"review merge ab12cd","expiresAt":"2026-10-15T00:05:00Z"},"summary":"merge feature:a into feature:b in the draft map"}"""),
            Ok("""{"result":{"written":["ovr_0123456789abcdef"],"skipped":[],"undecided":0},"sequence":3,"challenge":null,"summary":null}"""));

        var exit = await Run(api, interactive: true, "review merge ab12cd\n", "review", "merge", "feature:a", "--into", "feature:b");

        Assert.True(exit == 0, _error.ToString());
        Assert.Contains("merge feature:a into feature:b in the draft map", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"typed\":\"review merge ab12cd\"", api.Calls[1].Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lenses_and_filemap_build_their_queries_and_the_filemap_decisions_go_through_review()
    {
        var empty = new ScriptedApi(Problem(HttpStatusCode.NotFound, "SND-CONTEXT-044", "there is no draft map"));

        var lenses = await Lens(empty, "lenses", "feature:tabs.tab-manager", "--draft");
        var filemap = await Lens(new ScriptedApi(Problem(HttpStatusCode.NotFound, "SND-CONTEXT-045", "not a repo")), "filemap", "--repo", "nope", "--path", "src/", "--unmapped");
        var bad = await Lens(new ScriptedApi(Ok("{}")), "filemap", "--path", "src/");

        Assert.Equal(("GET", "/sindri/context/lenses?anchor=feature%3Atabs.tab-manager&draft=true"), (empty.Calls[0].Method, empty.Calls[0].Route));
        Assert.Equal((1, 1, 2), (lenses, filemap, bad));
    }
}
