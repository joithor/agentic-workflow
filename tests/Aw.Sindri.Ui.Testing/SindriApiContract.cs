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
}
