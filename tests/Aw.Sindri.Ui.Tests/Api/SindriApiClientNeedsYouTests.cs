using System.Net;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The P4 routes over the canned handler: Sindri's wire format for needs-you, decisions, requests, Decide and FileRequest.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientNeedsYouTests
{
    private const string Dec = "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2d701";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (SindriApiClient Client, FakeSindriApi Server, FakeSindriHandler Handler) Reach(FakeSindriApi? server = null)
    {
        server ??= new FakeSindriApi();
        var handler = new FakeSindriHandler(server);
        return (new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(A.HumanToken), TimeProvider.System), server, handler);
    }

    [Fact]
    public async Task NeedsYou_reads_rows_in_the_apis_order_with_every_pinned_field()
    {
        var (client, _, _) = Reach();

        var list = (await client.NeedsYou(_api, Token)).Value;

        Assert.Equal(["decision:" + Dec, "failure:service:index", "profile:drift"], list.Rows.Select(r => r.Id), StringComparer.Ordinal);
        var decision = list.Rows[0];
        Assert.Equal(("decision", "decision · re-scope", "pulse", "Decide"), (decision.Kind, decision.Label, decision.Urgency, decision.PrimaryAction));
        Assert.Equal(new SubjectRefView("decision", Dec), decision.Subject);
        Assert.Equal(("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", "sindri-app"), (decision.Item, decision.Repo));
        Assert.Equal((true, "Recorded now; the agent acts on it when it next runs."), (decision.RecordedOnly, decision.Notice));
        Assert.Null(decision.AnswerKind);
        Assert.Equal(["Decide"], decision.Actions);
        Assert.Equal("sindri decide " + Dec + " <choice>", decision.Cli);
        Assert.Equal((new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "Re-scope: drop the iOS head from this item"), (decision.Since, decision.Title));
        var failure = list.Rows[1];
        Assert.Equal(("colour", "RunService"), (failure.Urgency, failure.PrimaryAction));
        Assert.Equal(["RunService", "PauseService"], failure.Actions);
        Assert.Equal((41L, false, false), (list.AsOf.Cursor, list.AsOf.Stalled, list.AsOf.Rebuilding));
    }

    [Fact]
    public async Task NeedsYou_reads_a_profile_row_with_a_null_subject()
    {
        var (client, server, _) = Reach();
        server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-profile-null-subject");

        var profile = (await client.NeedsYou(_api, Token)).Value.Rows[2];

        Assert.Equal(("profile:drift", "calm", "ApproveProfile"), (profile.Id, profile.Urgency, profile.PrimaryAction));
        Assert.Null(profile.Subject);
    }

    [Fact]
    public async Task NeedsYou_reads_a_profile_row_with_a_profile_subject()
    {
        var (client, _, _) = Reach();

        var profile = (await client.NeedsYou(_api, Token)).Value.Rows[2];

        Assert.Equal(new SubjectRefView("profile", "3f1c09"), profile.Subject);
    }

    [Fact]
    public async Task Decisions_sends_pending_and_item_as_query_parameters()
    {
        var (client, server, handler) = Reach();

        _ = await client.Decisions(_api, pending: true, "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", Token);
        _ = await client.Decisions(_api, pending: false, item: null, Token);
        _ = await client.Decisions(_api, pending: null, item: null, Token);

        Assert.Equal(["?pending=true&item=ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", "?pending=false", ""], handler.Requests.Select(r => r.Query), StringComparer.Ordinal);
        Assert.Equal<(bool?, string?)>([(true, "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306"), (false, null), (null, null)], server.DecisionQueries);
    }

    [Fact]
    public async Task Decisions_reads_options_recommended_and_the_cleared_line_of_decided_and_voided()
    {
        var (client, server, _) = Reach();

        var pending = (await client.Decisions(_api, pending: true, item: null, Token)).Value.Decisions[0];
        var decided = (await client.Decisions(_api, pending: false, item: null, Token)).Value.Decisions;

        Assert.Equal(["accept", "decline"], pending.Options.Select(o => o.Id), StringComparer.Ordinal);
        Assert.Equal([false, true], pending.Options.Select(o => o.RejectLike));
        Assert.Equal(("accept", "pending"), (pending.Recommended, pending.State));
        Assert.Equal(["decided", "voided"], decided.Select(d => d.State), StringComparer.Ordinal);
        Assert.Equal(("accept", "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2d701 accepted"), (decided[0].Choice, decided[0].Cleared));
        Assert.Empty(decided[0].Actions);
        Assert.Equal(1, server.DecisionQueries.Count(q => q.Pending == false));
    }

    [Fact]
    public async Task Requests_sends_state_and_item_as_query_parameters()
    {
        var (client, _, handler) = Reach();

        var list = (await client.Requests(_api, "accepted", "ITM-1", Token)).Value;

        Assert.Equal(["?state=accepted&item=ITM-1"], handler.Requests.Select(r => r.Query), StringComparer.Ordinal);
        Assert.Equal(2, list.Requests.Count);
    }

    [Fact]
    public async Task Decide_posts_choice_and_note_once_and_never_retries()
    {
        var (client, server, handler) = Reach();
        server.Decided = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-UI-099", "down");

        _ = await client.Decide(_api, Dec, new DecideRequest("accept", "go ahead"), Token);
        server.Decided = RouteAnswer.SpecShaped("decisions-id", "accepted");
        var accepted = await client.Decide(_api, Dec, new DecideRequest("accept", "go ahead"), Token);

        Assert.Equal(60L, accepted.Value.Sequence);
        Assert.Equal(2, handler.Requests.Count(r => r.Method == "POST" && r.Path == "/sindri/decisions/" + Dec));
        Assert.Equal([(Dec, new DecideRequest("accept", "go ahead")), (Dec, new DecideRequest("accept", "go ahead"))], server.DecideCalls);
    }

    [Theory]
    [InlineData("decided-elsewhere", "SND-DEC-001")]
    [InlineData("voided", "SND-VIEW-001")]
    public async Task Decide_maps_409_decided_elsewhere_and_voided_with_their_details(string scenario, string code)
    {
        var (client, server, _) = Reach();
        server.Decided = RouteAnswer.Problem("decisions-id", scenario);

        var result = await client.Decide(_api, Dec, new DecideRequest("accept", Note: null), Token);

        Assert.Equal((code, ErrorKind.Conflict), (result.Error.Code, result.Error.Kind));
        Assert.Contains("(fix: re-read the list)", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-found", "SND-DEC-404", ErrorKind.NotFound)]
    [InlineData("bad-choice", "SND-DEC-001", ErrorKind.Invalid)]
    public async Task Decide_maps_SND_DEC_404_SND_DEC_001_and_SND_VIEW_001(string scenario, string code, ErrorKind kind)
    {
        var (client, server, _) = Reach();
        server.Decided = RouteAnswer.Problem("decisions-id", scenario);

        var result = await client.Decide(_api, Dec, new DecideRequest("maybe", Note: null), Token);

        Assert.Equal((code, kind), (result.Error.Code, result.Error.Kind));
    }

    [Fact]
    public async Task FileRequest_returns_the_request_and_decision_ids()
    {
        var (client, server, _) = Reach();

        var filed = (await client.FileRequest(_api, new FileRequestRequest("accept-request", "ITM-1", "Please re-scope"), Token)).Value;

        Assert.Equal(("REQ-0192a1b2c3d4e5f6a7b8c9d0e1f2d706", "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2d707", 62L), (filed.RequestId, filed.DecisionId, filed.Accepted.Sequence));
        Assert.Equal([new FileRequestRequest("accept-request", "ITM-1", "Please re-scope")], server.FileCalls);
    }

    [Theory]
    [InlineData("invalid", "SND-REQ-001", ErrorKind.Invalid)]
    [InlineData("item-not-found", "SND-ITEM-404", ErrorKind.NotFound)]
    [InlineData("item-closed", "SND-ITEM-409", ErrorKind.Conflict)]
    public async Task FileRequest_maps_SND_REQ_001_SND_REQ_002_and_item_404_409(string scenario, string code, ErrorKind kind)
    {
        var (client, server, _) = Reach();
        server.Filed = RouteAnswer.Problem("requests", scenario);

        var result = await client.FileRequest(_api, new FileRequestRequest("accept-request", ItemId: null, ""), Token);

        Assert.Equal((code, kind), (result.Error.Code, result.Error.Kind));
    }

    [Fact]
    public async Task The_query_after_an_accepted_Decide_sends_X_Sindri_After()
    {
        var (client, _, handler) = Reach();

        _ = await client.Decide(_api, Dec, new DecideRequest("accept", Note: null), Token);
        _ = await client.NeedsYou(_api, Token);
        _ = await client.NeedsYou(_api, Token);

        Assert.Equal(["60", null], handler.Requests.Where(r => r.Path == "/sindri/needs-you").Select(r => r.After), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_query_after_an_accepted_FileRequest_sends_its_sequence()
    {
        var (client, _, handler) = Reach();

        _ = await client.FileRequest(_api, new FileRequestRequest("accept-request", ItemId: null, "x"), Token);
        _ = await client.Decisions(_api, pending: true, item: null, Token);

        Assert.Equal("62", handler.Requests.Single(r => r.Path == "/sindri/decisions").After);
    }

    [Fact]
    public async Task A_read_your_writes_timeout_SND_API_011_keeps_done_and_marks_stale()
    {
        var (client, server, _) = Reach(new FakeSindriApi { Lag = ViewsLag.Behind });

        var decided = await client.Decide(_api, Dec, new DecideRequest("accept", Note: null), Token);
        var behind = await client.NeedsYou(_api, Token);
        var next = await client.NeedsYou(_api, Token);

        Assert.True(decided.IsSuccess);
        Assert.True(SindriApiReads.IsViewsBehind(behind.Error));
        Assert.True(next.IsSuccess);
        _ = Assert.Single(server.DecideCalls);
    }

    [Fact]
    public async Task New_calls_send_human_ui_and_never_terminal_or_origin()
    {
        var (client, _, handler) = Reach();

        _ = await client.NeedsYou(_api, Token);
        _ = await client.Decisions(_api, pending: true, item: null, Token);
        _ = await client.Requests(_api, state: null, item: null, Token);
        _ = await client.Decide(_api, Dec, new DecideRequest("accept", Note: null), Token);
        _ = await client.FileRequest(_api, new FileRequestRequest("accept-request", ItemId: null, "x"), Token);

        Assert.Equal(5, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(("human", "ui", null, null), (r.Caller, r.Client, r.Terminal, r.Origin)));
    }
}
