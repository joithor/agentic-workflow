using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>A job's repo (API P6 review B1, B2): the request's for a human, copied onto the job's own events so the session filter hides them in other repos.</summary>
[Lane(TestLane.Unit)]
public sealed class JobRepoTests
{
    private static JobStarted Started(RealJobRuntime runtime) =>
        runtime.H.Events.Payload<JobStarted>(Assert.Single(runtime.Events, e => e.Envelope.Type == EventTypes.JobStarted)).Value;

    [Fact]
    public async Task Progress_and_finish_events_carry_the_repo_of_the_start()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();

        _ = await runtime.Start(new ProbeJobRequest("repo"), A.Caller().Human().Build());
        await runtime.Quiesce();

        Expect.Equal(RepoName.From("demo"), Started(runtime).Repo);
        var progress = runtime.Events.Where(e => e.Envelope.Type == EventTypes.JobProgress).Select(e => runtime.H.Events.Payload<JobProgress>(e).Value).ToList();
        Assert.NotEmpty(progress);   // the probe reports a line: an empty list would make the check below pass for nothing
        Assert.All(progress, p => Expect.Equal(RepoName.From("demo"), p.Repo));
        var finished = runtime.Events.Where(e => e.Envelope.Type == EventTypes.JobFinished).Select(e => runtime.H.Events.Payload<JobFinishedPublic>(e).Value).ToList();
        Expect.Equal(RepoName.From("demo"), Assert.Single(finished).Repo);
    }

    [Fact]
    public async Task A_humans_job_takes_the_requests_repo_not_the_checkout_they_stand_in()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();

        _ = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), A.Caller().Human().InRepo("demo").Build());
        await runtime.Quiesce();

        Assert.Null(Started(runtime).Repo);
        Assert.DoesNotContain(runtime.Events, e => e.Envelope.Type == EventTypes.JobProgress && runtime.H.Events.Payload<JobProgress>(e).Value.Repo is not null);
    }
}
