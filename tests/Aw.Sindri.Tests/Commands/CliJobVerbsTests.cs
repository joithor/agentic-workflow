using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListJobs;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri jobs', 'jobs show' and 'jobs cancel' (API P6 unit H): clients of the job routes.</summary>
public sealed class CliJobVerbsTests : IDisposable
{
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_list_sends_the_filters_and_prints_a_line_each()
    {
        var list = Body(new JobList([new JobSummary(JobId.From(Id), JobKind.From("RunService"), JobKey.From("heartbeat"), Item: null, Repo: null, ServiceName.From("heartbeat"),
            JobState.Failed, "human:local", KnownInstants.Epoch, KnownInstants.Epoch, Rounds: 4, "the service threw", "SND-SCHED-001")], Empty: null,
            new AsOf(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false)));
        var api = new JobApi().Ok("GET", "/sindri/jobs", list);

        var code = await Finished(_rig.RunJobs(api, "--state", "failed", "--kind", "RunService"));

        Assert.Equal(0, code);
        Assert.Equal(1, api.Count("GET /sindri/jobs?state=failed&kind=RunService"));
        Assert.Equal($"{Id}  failed  RunService  heartbeat  2026-01-01T00:00:00Z  4 lines  the service threw SND-SCHED-001\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_list_says_1_line_for_a_single_line()
    {
        var list = Body(new JobList([new JobSummary(JobId.From(Id), JobKind.From("RunService"), JobKey.From("heartbeat"), Item: null, Repo: null, ServiceName.From("heartbeat"),
            JobState.Succeeded, "human:local", KnownInstants.Epoch, KnownInstants.Epoch, Rounds: 1, "ok: worked", Code: null)], Empty: null,
            new AsOf(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false)));
        var api = new JobApi().Ok("GET", "/sindri/jobs", list);

        _ = await Finished(_rig.RunJobs(api));

        Assert.Equal($"{Id}  succeeded  RunService  heartbeat  2026-01-01T00:00:00Z  1 line  ok: worked\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_show_prints_the_job_and_its_lines()
    {
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, View(JobState.Running, rounds: 2));

        var code = await Finished(_rig.RunJobs(api, "show", Id));

        Assert.Equal(0, code);
        Assert.Equal($"{Id}  running  RunService  heartbeat\nstarted by human:local at 2026-01-01T00:00:00Z\nline 1\nline 2\ncancel: sindri jobs cancel {Id}\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_show_json_is_the_route_body()
    {
        var body = View(JobState.Succeeded, rounds: 1, summary: "ok");
        var api = new JobApi().Ok("GET", "/sindri/jobs/" + Id, body);

        var code = await Finished(_rig.RunJobs(api, "show", Id, "--json"));

        Assert.Equal((0, body + "\n"), (code, _rig.Out.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Jobs_cancel_prints_the_sequence()
    {
        var api = new JobApi().Ok("POST", "/sindri/jobs/" + Id + "/cancel", Body(new Core.Commands.CommandAccepted(CommandId.From(new Guid(8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), 12)));

        var code = await Finished(_rig.RunJobs(api, "cancel", Id));

        Assert.Equal((0, $"{Id} cancel requested (sequence 12)\n"), (code, _rig.Out.ToString().ReplaceLineEndings("\n")));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("show")]
    [InlineData("cancel")]
    [InlineData("--wat")]
    [InlineData("show", "a", "b")]
    public async Task A_bad_command_line_is_refused_without_calling(params string[] args)
    {
        var api = new JobApi();

        var code = await Finished(_rig.RunJobs(api, args));

        Assert.Equal(2, code);
        Assert.Empty(api.Calls);
    }
}
