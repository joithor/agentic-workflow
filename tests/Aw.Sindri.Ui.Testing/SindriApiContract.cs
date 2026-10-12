using System.Net;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="ISindriApi"/> passes: the in-memory fake and the real HTTP client over the canned
/// handler. A binding derives from it and says how to reach a <see cref="FakeSindriApi"/> server.
/// </summary>
public abstract class SindriApiContract
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private protected abstract ISindriApi Reach(FakeSindriApi server);

    [Fact]
    public async Task Ping_answers_the_pid_and_epoch()
    {
        var server = new FakeSindriApi { Pong = new PingView(99, 3) };

        var ping = await Reach(server).Ping(_api, Token);

        Expect.Equal(new PingView(99, 3), ping.Value);
    }

    [Fact]
    public async Task A_failing_ping_is_unreachable_while_the_status_still_answers()
    {
        var server = new FakeSindriApi { FailPing = true };
        var api = Reach(server);

        Expect.Equal("SND-UI-002", (await api.Ping(_api, Token)).Error.Code);
        Expect.Equal(4242, (await api.Status(_api, Token)).Value.Pid);
    }

    [Fact]
    public async Task Status_answers_the_lease_profile_and_services()
    {
        var server = new FakeSindriApi { Current = A.Status(pid: 5, epoch: 2, A.ChangedProfile(), A.Heartbeat(A.Noon, "7s")) };

        var status = (await Reach(server).Status(_api, Token)).Value;

        Expect.Equal((5, 2L), (status.Pid, status.Epoch));
        Expect.Equal(A.ChangedProfile(), status.Profile);
        Expect.Sequence([A.Heartbeat(A.Noon, "7s")], status.Services);
    }

    // API spec "Versioning": P1 adds apiVersion to the status; S1's status has none, which the app reads as no mismatch.
    [Fact]
    public async Task Status_carries_the_api_version_when_sindri_reports_one()
    {
        var server = new FakeSindriApi { Current = A.Status() with { ApiVersion = 2 } };
        var api = Reach(server);

        Expect.Equal(2, (await api.Status(_api, Token)).Value.ApiVersion);

        server.Current = A.Status();
        Expect.Equal(expected: null, (await api.Status(_api, Token)).Value.ApiVersion);
    }

    [Fact]
    public async Task Events_after_a_cursor_come_in_order_and_end_with_the_connection()
    {
        var server = new FakeSindriApi();
        server.Push(A.Event(1), A.Event(2), A.Event(3, "profile.approved"));
        var seen = new List<EventView>();

        await foreach (var e in Reach(server).Events(_api, after: 1, Token))
        {
            seen.Add(e);
            if (seen.Count == 2)
            {
                server.Drop();
            }
        }

        Expect.Sequence([A.Event(2), A.Event(3, "profile.approved")], seen);
    }

    // API §6 and §3.6: a refused call (403 ProblemDetails, e.g. SND-AUTH-001 for a missing or stale token) is Sindri's
    // code and detail as they are, a Forbidden error; never SND-UI-003, which is for answers this build can't read.
    [Fact]
    public async Task A_refused_call_is_sindris_code_and_detail_as_they_are()
    {
        var server = new FakeSindriApi { Refusal = A.NoTokenRefusal };
        var api = Reach(server);

        var ping = await api.Ping(_api, Token);
        var status = await api.Status(_api, Token);

        Expect.Equal(new Error(A.NoTokenRefusal.Code, ErrorKind.Forbidden, A.NoTokenRefusal.Detail), ping.Error);
        Expect.Equal(new Error(A.NoTokenRefusal.Code, ErrorKind.Forbidden, A.NoTokenRefusal.Detail), status.Error);
        Expect.Sequence([], await api.Events(_api, 0, Token).ToListAsync(Token));
    }

    [Fact]
    public async Task A_server_that_is_down_is_unreachable()
    {
        var server = new FakeSindriApi();
        server.GoDown();
        var api = Reach(server);

        var ping = await api.Ping(_api, Token);
        var status = await api.Status(_api, Token);

        Expect.Equal("SND-UI-002", ping.Error.Code);
        Expect.Equal("SND-UI-002", status.Error.Code);
        Expect.Sequence([], await api.Events(_api, 0, Token).ToListAsync(Token));
    }

    // GET /sindri/commands (API P1): the catalog as Sindri's golden serves it.
    [Fact]
    public async Task Commands_answer_the_catalog_rows_with_their_ui_status()
    {
        var catalog = (await Reach(new FakeSindriApi()).Commands(_api, Token)).Value;

        Expect.Equal(18, catalog.Rows.Count);
        Expect.Equal("ready", catalog.Rows.Single(r => r.Action == "RebuildViews").Ui);
        Expect.Equal("sindri profile approve [<hash>]", catalog.Rows.Single(r => r.Action == "ApproveProfile").Cli);
    }

    // GET /sindri/services (API P2, spec-shaped): U1 reads the view's EmptyText sentence and its asOf.
    [Fact]
    public async Task Services_answer_their_empty_sentence_and_freshness()
    {
        var server = new FakeSindriApi { ServiceView = RouteAnswer.SpecShaped("services", "stalled") };

        var view = (await Reach(server).Services(_api, Token)).Value;

        Expect.Equal("Views are stalled at 42: run `sindri views rebuild`", view.Empty);
        Expect.Equal((41L, 42L, true, false), (view.AsOf.Cursor, view.AsOf.LedgerHead, view.AsOf.Stalled, view.AsOf.Rebuilding));
    }

    // POST /sindri/views/rebuild (API P2, spec-shaped): accepted with the sequence the next query waits for.
    [Fact]
    public async Task Rebuild_views_answers_the_accepted_sequence()
    {
        var accepted = (await Reach(new FakeSindriApi()).RebuildViews(_api, Token)).Value;

        Expect.Equal(43L, accepted.Sequence);
    }

    // UI spec §4.1 Error: a refusal is the API's title (the SND code) and detail as served, never the app's words.
    [Fact]
    public async Task A_refused_command_is_the_apis_code_and_detail()
    {
        var server = new FakeSindriApi { Rebuild = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-009", "Sindri is busy with another command (fix: retry)") };

        var refused = await Reach(server).RebuildViews(_api, Token);

        Expect.Equal(("SND-API-009", "Sindri is busy with another command (fix: retry)"), (refused.Error.Code, refused.Error.Message));
    }

    // GET /sindri/queue (API P3, spec-shaped): every group in the API's order, empty ones included with their own sentence (E-18).
    [Fact]
    public async Task Queue_answers_every_group_in_order_with_its_own_empty_sentence()
    {
        var queue = (await Reach(new FakeSindriApi()).Queue(_api, Token)).Value;

        Expect.Sequence(["waitingOnYou", "withYou", "running", "queued"], queue.Groups.Select(g => g.Group));
        Expect.Sequence([0, 0, 0, 4], queue.Groups.Select(g => g.Items.Count));
        Expect.Equal("Nothing is waiting on you.", queue.Groups[0].Empty);
        Expect.Equal("Nothing running.", queue.Groups[2].Empty);
        Expect.True(queue.Groups.SelectMany(g => g.Items).All(i => i.PrimaryAction is null));
        Expect.Equal(expected: null, queue.Groups[3].Empty);
        Expect.Equal(("new", "Try the queue"), (queue.Groups[3].Items[3].State, queue.Groups[3].Items[3].Title));
    }

    // An empty queue: the view's own sentence, and each group's.
    [Fact]
    public async Task An_empty_queue_answers_the_views_sentence()
    {
        var queue = (await Reach(new FakeSindriApi { QueueView = RouteAnswer.SpecShaped("queue", "empty") }).Queue(_api, Token)).Value;

        Expect.True(queue.Groups.All(g => g.Items.Count == 0 && g.Empty is { Length: > 0 }));
        Expect.Equal("Nothing in the queue. Add one with `sindri item add`.", queue.Empty);
    }

    // GET /sindri/items/{id} (API P3, spec-shaped): the version CloseItem sends back, and the actions allowed now.
    [Fact]
    public async Task An_item_answers_its_version_and_actions()
    {
        var item = (await Reach(new FakeSindriApi()).Item(_api, A.NewItem, Token)).Value;

        Expect.Equal((A.NewItem, 1L, "human:cli"), (item.Id, item.Version, item.CreatedBy));
        Expect.Sequence(["CloseItem:done", "CloseItem:cancelled"], item.Actions);
        Expect.Equal(("Check the queue shows a new item without a click.", "linear:issue/ENG-142"), (item.Body, item.Link));
    }

    // UI spec §4.9 Item detail: an unknown item is the API's SND-ITEM-404 with its detail as served.
    [Fact]
    public async Task An_unknown_item_is_the_apis_SND_ITEM_404()
    {
        var missing = await Reach(new FakeSindriApi()).Item(_api, A.MissingItem, Token);

        Expect.Equal(("SND-ITEM-404", A.Detail("items-id", "not-found")), (missing.Error.Code, missing.Error.Message));
    }

    // GET /sindri/items/{id}/timeline?after= (API P3, spec-shaped): a page and the cursor of the next; the last page has none.
    [Fact]
    public async Task The_timeline_pages_by_its_next_cursor()
    {
        var api = Reach(new FakeSindriApi());

        var first = (await api.Timeline(_api, A.NewItem, after: 0, Token)).Value;
        var second = (await api.Timeline(_api, A.NewItem, after: first.Next!.Value, Token)).Value;

        Expect.Equal(100, first.Entries.Count);
        Expect.Equal(151L, first.Next);
        Expect.Sequence([152L], second.Entries.Select(e => e.Sequence));
        Expect.Equal(expected: null, second.Next);
    }

    // POST /sindri/items (CreateItem, desk, API P3): the body as sent, and the new id with the accepted sequence.
    [Fact]
    public async Task Create_item_sends_the_form_and_answers_the_new_id()
    {
        var server = new FakeSindriApi();

        var created = (await Reach(server).CreateItem(_api, new CreateItemRequest("Proof: appears live", "agentic-workflow", "A body", Link: null), Token)).Value;

        Expect.Equal(("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", 58L), (created.ItemId, created.Accepted.Sequence));
        Expect.Sequence([new CreateItemRequest("Proof: appears live", "agentic-workflow", "A body", Link: null)], server.Created);
    }

    // POST /sindri/items/{id}/close (CloseItem, desk, API P3): the reason and the version the human saw.
    [Fact]
    public async Task Close_item_sends_the_reason_and_the_version_seen()
    {
        var server = new FakeSindriApi();

        var accepted = (await Reach(server).CloseItem(_api, A.NewItem, new CloseItemRequest("cancelled", 1), Token)).Value;

        Expect.Equal(59L, accepted.Sequence);
        Expect.Sequence([(A.NewItem, new CloseItemRequest("cancelled", 1))], server.Closed);
    }

    // UI spec §9.1: a stale version is the API's SND-ITEM-409 with its detail as served.
    [Fact]
    public async Task Close_with_a_stale_version_is_the_apis_SND_ITEM_409()
    {
        var server = new FakeSindriApi { Close = RouteAnswer.Problem("items-id-close", "stale-version") };

        var refused = await Reach(server).CloseItem(_api, A.NewItem, new CloseItemRequest("done", 1), Token);

        Expect.Equal(("SND-ITEM-409", A.Detail("items-id-close", "stale-version")), (refused.Error.Code, refused.Error.Message));
    }
}
