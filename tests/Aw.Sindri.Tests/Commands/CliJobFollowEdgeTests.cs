using System.Net;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host.Commands;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The follower's branches off the happy path (API P6 review T11, C7, C10): the cancel grace running out, an error frame, Ctrl-C while polling, a lone second Ctrl-C, a refused stream, a polling follower that rides out a lagging view, and a second Ctrl-C while the cancel is in flight.</summary>
public sealed class CliJobFollowEdgeTests : IDisposable
{
    private const string CancelRoute = "POST /sindri/jobs/" + Id + "/cancel";
    private const string JobRoute = "/sindri/jobs/" + Id;
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static CommandAccepted Accepted() => new(CommandId.From(new Guid(8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), 12);

    private static JobApi Api() => new JobApi().Ok("POST", "/sindri/services/heartbeat/run", Body(Started)).Ok("POST", "/sindri/jobs/" + Id + "/cancel", Body(Accepted()));

    private static JobApi Polling() => new JobApi { StreamsFull = true }.Ok("POST", "/sindri/services/heartbeat/run", Body(Started)).Ok("POST", "/sindri/jobs/" + Id + "/cancel", Body(Accepted()));

    [Fact, Trait("Lane", "unit")]
    public async Task The_cancel_grace_running_out_says_the_job_may_still_be_running_and_exits_1()
    {
        var api = Api();
        var run = _rig.RunServices(api, "run", "heartbeat");
        await api.Until(() => api.Streams == 1);

        await _rig.Interrupt();
        await api.Until(() => api.Count(CancelRoute) == 1);
        await _rig.Clock.TimersReached(1);   // the 5 s wait for the job's own end
        _rig.Clock.Advance(JobFollower.CancelGrace);

        Assert.Equal(1, await Finished(run));
        Assert.EndsWith("cancel requested; the job may still be running\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Equal(1, api.Count(CancelRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_error_frame_falls_back_to_reading_the_job()
    {
        var api = Api().Ok("GET", JobRoute, View(Core.Jobs.JobState.Succeeded, rounds: 1, summary: "ok: 1 line"));
        var run = _rig.RunServices(api, "run", "heartbeat");
        await api.Until(() => api.Streams == 1);

        await api.Frame(11, "error", "{}");
        await _rig.AdvanceOnTimers(JobFollower.Poll, run);

        Assert.Equal(0, await Finished(run));
        Assert.Equal($"job {Id} started\nline 1\nsucceeded: ok: 1 line\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
        Assert.Equal(1, api.Count("GET " + JobRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Ctrl_c_while_polling_cancels_once_and_leaves_when_the_grace_runs_out()
    {
        var api = Polling().Ok("GET", JobRoute, View(Core.Jobs.JobState.Running, rounds: 1));
        var run = _rig.RunServices(api, "run", "heartbeat");
        await _rig.Clock.TimersReached(1);   // the first poll wait

        await _rig.Interrupt();
        await _rig.AdvanceOnTimers(JobFollower.Poll, run);

        Assert.Equal(1, await Finished(run));
        Assert.EndsWith("cancel requested; the job may still be running\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Equal(1, api.Count(CancelRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_ctrl_c_with_no_first_leaves_the_job_running_and_cancels_nothing()
    {
        var api = Api();
        var run = _rig.RunServices(api, "run", "heartbeat");
        await api.Until(() => api.Streams == 1);

        await _rig.SecondInterrupt();

        Assert.Equal(1, await Finished(run));
        Assert.EndsWith("left the job running\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Equal(0, api.Count(CancelRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stream_refused_for_another_reason_than_a_full_list_is_an_error_not_a_poll()
    {
        var api = new JobApi { StreamRefused = (HttpStatusCode.Forbidden, "SND-AUTH-001") }.Ok("POST", "/sindri/services/heartbeat/run", Body(Started));
        var run = _rig.RunServices(api, "run", "heartbeat");

        var code = await Finished(run);

        Assert.Equal(2, code);
        Assert.Contains("SND-AUTH-001", _rig.Error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, api.Count("GET " + JobRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Polling_asks_for_the_views_at_the_start_sequence_and_rides_out_a_lagging_view_and_a_timeout()
    {
        var api = Polling().On("GET", JobRoute, Refused(HttpStatusCode.ServiceUnavailable, "SND-API-011", "behind"), Refused(HttpStatusCode.GatewayTimeout, "SND-API-002", "slow"),
            new Reply(HttpStatusCode.OK, View(Core.Jobs.JobState.Succeeded, rounds: 1, summary: "ok: 1 line")));
        var run = _rig.RunServices(api, "run", "heartbeat");

        await _rig.AdvanceOnTimers(JobFollower.Poll, run);

        Assert.Equal(0, await Finished(run));
        Assert.Equal(3, api.Count("GET " + JobRoute));
        Assert.All(api.JobAfters, after => Assert.Equal("10", after));   // the sequence of the start's answer
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_ctrl_c_is_seen_while_the_cancel_request_is_in_flight()
    {
        var api = Api();
        var inFlight = api.Hold(CancelRoute);
        try
        {
            var run = _rig.RunServices(api, "run", "heartbeat");
            await api.Until(() => api.Streams == 1);

            await _rig.Interrupt();
            await api.Until(() => api.Count(CancelRoute) == 1);   // the POST is held: the server has not answered
            await _rig.SecondInterrupt();

            Assert.Equal(1, await Finished(run));
            Assert.EndsWith("cancel requested; the job may still be running\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
        }
        finally
        {
            _ = inFlight.TrySetResult();
        }
    }
}
