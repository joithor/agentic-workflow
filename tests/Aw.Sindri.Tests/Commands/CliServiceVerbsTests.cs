using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListServices;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri services', 'services run --detach', 'pause' and 'resume' (API P6 unit H): clients of the service routes.</summary>
public sealed class CliServiceVerbsTests : IDisposable
{
    private static readonly AsOf _fresh = new(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false);
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static string List() => Body(new ListServicesResponse(
    [
        new ServiceView(ServiceName.From("heartbeat"), "5s", Paused: false, new LastRun(KnownInstants.Epoch, ServiceOutcome.Ok, Code: null, KnownInstants.Epoch), KnownInstants.Epoch.AddSeconds(5)),
        new ServiceView(ServiceName.From("sync"), "1h", Paused: true, new LastRun(KnownInstants.Epoch, ServiceOutcome.Failed, "SND-SCHED-001", KnownInstants.Epoch), NextSlot: null, JobId.From(Id)),
        new ServiceView(ServiceName.From("fresh"), "24h", Paused: false, LastRun: null, NextSlot: null),
    ], Empty: null, _fresh));

    [Fact, Trait("Lane", "unit")]
    public async Task Services_list_prints_paused_and_next()
    {
        var api = new JobApi().Ok("GET", "/sindri/services", List());

        var code = await Finished(_rig.RunServices(api, []));

        Assert.Equal(0, code);
        Assert.Equal(
            "heartbeat  every 5s  last: ok  next: 2026-01-01T00:00:05Z\n"
            + $"sync  every 1h  paused  last: failed SND-SCHED-001  next: paused  running {Id}\n"
            + "fresh  every 24h  last: never  next: -\n",
            _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_json_equals_route_body()
    {
        var list = List();
        var api = new JobApi().Ok("GET", "/sindri/services", list).Ok("POST", "/sindri/services/heartbeat/run", Body(Started))
            .Ok("GET", "/sindri/jobs/" + Id, View(Core.Jobs.JobState.Succeeded, rounds: 1, summary: "ok"));

        var listed = await Finished(_rig.RunServices(api, "--json"));
        var listText = _rig.Out.ToString();
        var run = _rig.RunServices(api, "run", "heartbeat", "--json");
        await FollowRig.WaitUntil(() => api.Streams == 1);
        await api.Frame(11, "job.finished", Finished("succeeded", "ok"));
        var ran = await Finished(run);

        Assert.Equal((0, 0), (listed, ran));
        Assert.Equal(list + "\n", listText);
        Assert.Equal(list + "\n" + Body(Started) + "\n" + View(Core.Jobs.JobState.Succeeded, rounds: 1, summary: "ok") + "\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Detach_prints_job_started()
    {
        var api = new JobApi().Ok("POST", "/sindri/services/heartbeat/run", Body(Started));

        var code = await Finished(_rig.RunServices(api, "run", "heartbeat", "--detach"));

        Assert.Equal(0, code);
        Assert.Equal($"job {Id} started\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
        Assert.Equal(0, api.Streams);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Detach_json_prints_the_job_started_body()
    {
        var api = new JobApi().Ok("POST", "/sindri/services/heartbeat/run", Body(Started));

        var code = await Finished(_rig.RunServices(api, "run", "heartbeat", "--detach", "--json"));

        Assert.Equal((0, Body(Started) + "\n"), (code, _rig.Out.ToString().ReplaceLineEndings("\n")));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("pause", "heartbeat paused (sequence 12)")]
    [InlineData("resume", "heartbeat resumed (sequence 12)")]
    public async Task Pause_and_resume_print_the_sequence(string verb, string expected)
    {
        var api = new JobApi().Ok("POST", "/sindri/services/heartbeat/" + verb, Body(new Core.Commands.CommandAccepted(CommandId.From(new Guid(8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), 12)));

        var code = await Finished(_rig.RunServices(api, verb, "heartbeat"));

        Assert.Equal((0, expected + "\n"), (code, _rig.Out.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_run_names_the_code_and_exits_2()
    {
        var api = new JobApi().On("POST", "/sindri/services/heartbeat/run", Refused(HttpStatusCode.Forbidden, "SND-AUTH-002", "a session may not run a service"));

        var code = await Finished(_rig.RunServices(api, "run", "heartbeat"));

        Assert.Equal(2, code);
        Assert.StartsWith("SND-AUTH-002 ", _rig.Error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, api.Streams);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("run")]
    [InlineData("pause")]
    [InlineData("run", "a", "b")]
    [InlineData("wat")]
    public async Task A_bad_command_line_is_refused_without_calling(params string[] args)
    {
        var api = new JobApi();

        var code = await Finished(_rig.RunServices(api, args));

        Assert.Equal(2, code);
        Assert.Empty(api.Calls);
    }
}
