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
}
