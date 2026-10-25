using System.Text.Json;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Text;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>What 'context map build' prints when its job ends: the draft line and each degraded reason (clusters included), so a lost cluster pass is never silent.</summary>
public sealed class MapBuildJobTextTests : IDisposable
{
    private static readonly string[] _reasons = ["clusters: timed out after 60 min", "labels"];
    private readonly Commands.JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static BuildContextMapResult Result(params string[] degraded) => new("web-app", 7, 42, degraded);

    [Fact, Trait("Lane", "unit")]
    public void A_build_with_degraded_parts_prints_one_line_each_with_the_reason()
    {
        Assert.Equal("draft 7 for web-app · 42 proposed\ndegraded: clusters: timed out after 60 min\ndegraded: labels\n", MapBuildJobText.Render(Result(_reasons)).ReplaceLineEndings("\n"));
        Assert.Equal("draft 7 for web-app · 42 proposed\n", MapBuildJobText.Render(Result()).ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_followed_build_prints_the_reasons_after_its_succeeded_line()
    {
        var api = new Commands.JobApi().Ok("POST", "/sindri/context/map/build", Body(Started))
            .Ok("GET", "/sindri/jobs/" + Id, View(JobState.Succeeded, rounds: 1, summary: "draft 7", result: JsonSerializer.SerializeToElement(Result(_reasons), SliceHarness.Json)));
        var run = Task.Run(async () => await new ContextJobVerbs(Client(api), _rig.Output(), _rig.Follower(api)).Run(["map", "build"], _rig.Interrupted, _rig.Again));
        await api.Until(() => api.Streams == 1);
        await api.Frame(11, "job.finished", Finished("succeeded", "draft 7"));

        var exit = await Finished(run);

        Assert.Equal(0, exit);
        Assert.Contains("succeeded: draft 7\ndraft 7 for web-app · 42 proposed\ndegraded: clusters: timed out after 60 min\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }
}
