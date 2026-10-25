using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Commands;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'context sources sync' and 'context map build' without --detach: they follow the job they started, as 'services run' does.</summary>
public sealed class ContextJobFollowTests : IDisposable
{
    private readonly JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Theory, Trait("Lane", "unit")]
    [InlineData("/sindri/context/sources/sync", "sources", "sync")]
    [InlineData("/sindri/context/map/build", "map", "build")]
    public async Task Without_detach_the_verb_follows_the_started_job_to_its_result(string route, params string[] args)
    {
        var api = new JobApi().Ok("POST", route, Body(Started)).Ok("GET", "/sindri/jobs/" + Id, View(JobState.Succeeded, rounds: 2, summary: "done"));   // a build reads the final job for its result
        var verbs = new ContextJobVerbs(Client(api), _rig.Output(), _rig.Follower(api));
        var run = Task.Run(() => verbs.Run(args, _rig.Interrupted, _rig.Again), TestContext.Current.CancellationToken);
        await api.Until(() => api.Streams == 1);

        await api.Frame(11, "job.progress", Progress(1, "reading"));
        await api.Frame(12, "job.finished", Finished("succeeded", "1 source(s) synced"));

        Assert.Equal(0, await Finished(run));
        Assert.Equal($"job {Id} started\nreading\nsucceeded: 1 source(s) synced\n", _rig.Out.ToString().ReplaceLineEndings("\n"));
    }
}
