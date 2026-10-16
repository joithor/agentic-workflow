using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'jobs' view fold through the projector's pages (API P6 unit A): the four job events make and change one row per job.</summary>
[Lane(TestLane.Unit)]
public sealed class JobsProjectionTests
{
    private readonly ViewsHarness _v = new();

    private SliceHarness H => _v.Slice;

    private async Task<JobRow?> Row() =>
        (await _v.Views.Get<JobRow>(ViewNames.Jobs, A.FirstJobId.Value, TestContext.Current.CancellationToken)).Value;

    private Task Start() => _v.Fold(EventTypes.JobStarted,
        new JobStarted(H.Ids.New<CommandId>(), A.FirstJobId, JobKind.From("RunService"), JobKey.From("heartbeat"), Item: null, Repo: null, ServiceName.From("heartbeat")));

    [Fact]
    public async Task The_events_of_a_job_fold_into_one_row()
    {
        await Start();
        await _v.Fold(EventTypes.JobProgress, new JobProgress(H.Ids.New<CommandId>(), A.FirstJobId, 1, "working"));
        await _v.Fold(EventTypes.JobFinished, new JobFinished(H.Ids.New<CommandId>(), A.FirstJobId, JobOutcome.Succeeded, "ok: worked", Code: null, Result: null));

        var row = await Row();

        Expect.Equal((JobState.Succeeded, 1, 3L), (row!.State, row.Rounds, row.Sequence));
        Expect.Equal("working", Assert.Single(row.Progress).Line);
    }

    [Fact]
    public async Task A_cancel_ends_the_row()
    {
        await Start();
        await _v.Fold(EventTypes.JobCancelled, new JobCancelled(H.Ids.New<CommandId>(), A.FirstJobId, "human:cli"));

        Expect.Equal((JobState.Cancelled, "human:cli"), ((await Row())!.State, (await Row())!.CancelledBy));
    }

    [Fact]
    public async Task A_progress_line_for_a_job_that_never_started_stalls_the_projector()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = (await _v.Views.Open(ViewSchema.Version, ct)).Value;
        var orphan = Recorded.Of(H.Events, EventTypes.JobProgress, new JobProgress(H.Ids.New<CommandId>(), A.FirstJobId, 1, "lost"), 1);

        var applied = (await _v.Views.Apply([orphan], [new JobsProjection(H.Events)], H.Clock.GetUtcNow(), ct)).Value;

        Assert.NotNull(applied.Stall);
    }
}
