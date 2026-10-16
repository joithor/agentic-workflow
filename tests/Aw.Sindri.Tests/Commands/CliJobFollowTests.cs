using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Commands;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri services run' following its job (API P6 unit H): progress off the event stream, the result, Ctrl-C, polling when the streams are full, the process going away.</summary>
public sealed class CliJobFollowTests : IDisposable
{
    private const string CancelRoute = "POST /sindri/jobs/" + Id + "/cancel";
    private const string JobRoute = "/sindri/jobs/" + Id;
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static JobApi Api() => new JobApi().Ok("POST", "/sindri/services/heartbeat/run", Body(Started));

    [Fact, Trait("Lane", "unit")]
    public async Task Run_prints_progress_and_the_result()
    {
        var api = Api();
        var run = _rig.RunServices(api, "run", "heartbeat");
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Frame(11, "job.progress", Progress(1, "reading"));
        await api.Frame(12, "job.progress", Progress(1, "somebody else's line").Replace(Id, "JOB-ffffffffffffffffffffffffffffffff", StringComparison.Ordinal));
        await api.Frame(13, "job.progress", Progress(2, "done reading"));
        await api.Frame(14, "job.finished", Finished("succeeded", "ok: 3 files"));

        Assert.Equal(0, await Finished(run));
        Assert.Equal($"job {Id} started\nreading\ndone reading\nsucceeded: ok: 3 files\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
        Assert.Equal("/sindri/events?after=10&types=job.*", api.StreamQuery);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_job_exits_1_and_names_its_code()
    {
        var api = Api();
        var run = _rig.RunServices(api, "run", "heartbeat");
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Frame(11, "job.finished", Finished("failed", "the service threw", "SND-SCHED-001"));

        Assert.Equal(1, await Finished(run));
        Assert.EndsWith("failed: the service threw SND-SCHED-001\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Ctrl_c_cancels_and_prints_cancelled()
    {
        var api = Api().Ok("POST", "/sindri/jobs/" + Id + "/cancel", Body(new Core.Commands.CommandAccepted(Core.Ids.CommandId.From(new Guid(8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), 12)));
        var run = _rig.RunServices(api, "run", "heartbeat");
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await _rig.Interrupt();
        await FollowRig.WaitUntil(() => api.Count(CancelRoute) == 1);
        await api.Frame(12, "job.cancelled", Cancelled());

        Assert.Equal(1, await Finished(run));
        Assert.EndsWith("cancelled\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Equal(1, api.Count(CancelRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Second_ctrl_c_exits_at_once()
    {
        var api = Api().Ok("POST", "/sindri/jobs/" + Id + "/cancel", Body(new Core.Commands.CommandAccepted(Core.Ids.CommandId.From(new Guid(8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), 12)));
        var run = _rig.RunServices(api, "run", "heartbeat");
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await _rig.Interrupt();
        await FollowRig.WaitUntil(() => api.Count(CancelRoute) == 1);
        await _rig.SecondInterrupt();   // no frame, and the clock never moves: the 5 s wait for the cancel is not waited out

        Assert.Equal(1, await Finished(run));
        Assert.Equal(1, api.Count(CancelRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_falls_back_to_polling_on_429()
    {
        var api = new JobApi { StreamsFull = true }.Ok("POST", "/sindri/services/heartbeat/run", Body(Started))
            .Ok("GET", JobRoute, View(JobState.Running, rounds: 1), View(JobState.Running, rounds: 2), View(JobState.Succeeded, rounds: 3, summary: "ok: 3 lines"));
        var run = _rig.RunServices(api, "run", "heartbeat");

        await _rig.Tick(JobFollower.Poll, () => run.IsCompleted);

        Assert.Equal(0, await Finished(run));
        Assert.Equal($"job {Id} started\nline 1\nline 2\nline 3\nsucceeded: ok: 3 lines\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
        Assert.Equal(3, api.Count("GET " + JobRoute));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_exits_1_when_the_process_stops()
    {
        var api = Api();
        var run = _rig.RunServices(api, "run", "heartbeat");
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Frame(11, "job.progress", Progress(1, "reading"));
        await api.End();

        Assert.Equal(1, await Finished(run));
        Assert.Contains("not running", _rig.Error.ToString(), StringComparison.Ordinal);
    }
}
