using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>A job's own events carry the repo the job started with (API P6 review B1), so the session filter that hides 'job.started' hides them too.</summary>
[Lane(TestLane.Unit)]
public sealed class JobRepoTests
{
    [Fact]
    public async Task Progress_and_finish_events_carry_the_repo_of_the_start()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();

        _ = await runtime.Start(new EchoJobRequest(Lines: 2, DelayMs: 0), A.Caller().Human().InRepo("demo").Build());
        await runtime.Quiesce();

        var progress = runtime.Events.Where(e => e.Envelope.Type == EventTypes.JobProgress).Select(e => runtime.H.Events.Payload<JobProgress>(e).Value).ToList();
        var finished = runtime.Events.Where(e => e.Envelope.Type == EventTypes.JobFinished).Select(e => runtime.H.Events.Payload<JobFinishedPublic>(e).Value).ToList();
        Expect.Equal(2, progress.Count);
        Assert.All(progress, p => Expect.Equal(RepoName.From("demo"), p.Repo));
        Expect.Equal(RepoName.From("demo"), Assert.Single(finished).Repo);
    }
}
