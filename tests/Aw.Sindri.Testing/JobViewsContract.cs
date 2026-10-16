using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every IJobViews honours (API P6 review T9): a missing job reads as none; Running is the running jobs, oldest start first; List is the jobs
/// in a state and of a kind (either may be any), newest start first; a start time ties on the job id. The order follows the start, never the latest activity.
/// </summary>
public abstract class JobViewsContract
{
    private static readonly JobKind _run = JobKind.From("RunService");
    private static readonly JobKind _sync = JobKind.From("SyncSources");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh view with no jobs.</summary>
    /// <returns>The subject.</returns>
    protected abstract ValueTask<IJobViewsSubject> Create();

    private static JobId Id(int n) => JobId.From("JOB-" + n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture));

    private static DateTimeOffset At(int minutes) => KnownInstants.Epoch.AddMinutes(minutes);

    /// <summary>A view with no jobs reads as none.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_view_reads_as_no_jobs()
    {
        await using var subject = await Create();

        Expect.True((await subject.Views.Get(Id(1), Token)).Value is null);
        Expect.Equal(0, (await subject.Views.Running(Token)).Value.Count);
        Expect.Equal(0, (await subject.Views.List(state: null, kind: null, Token)).Value.Count);
    }

    /// <summary>A job reads back by id with the state it is in.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Get_reads_a_job_by_id_in_its_state()
    {
        await using var subject = await Create();
        await subject.Start(Id(1), _run, JobKey.From("a"), At(1));
        await subject.Start(Id(2), _run, JobKey.From("b"), At(2));
        await subject.Finish(Id(1));

        Expect.Equal(JobState.Succeeded, (await subject.Views.Get(Id(1), Token)).Value!.State);
        Expect.Equal(JobState.Running, (await subject.Views.Get(Id(2), Token)).Value!.State);
        Expect.True((await subject.Views.Get(Id(9), Token)).Value is null);
    }

    /// <summary>Running is the running jobs only, the oldest start first.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Running_is_the_running_jobs_oldest_start_first()
    {
        await using var subject = await Create();
        await subject.Start(Id(3), _run, JobKey.From("c"), At(1));
        await subject.Start(Id(1), _run, JobKey.From("a"), At(2));
        await subject.Start(Id(2), _sync, JobKey.From("b"), At(3));
        await subject.Finish(Id(2));

        var running = (await subject.Views.Running(Token)).Value;

        Expect.Sequence([Id(3), Id(1)], running.Select(static r => r.Id));
    }

    /// <summary>List is newest start first: a job that started earlier but ended just now is not above a job that started later.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task List_is_newest_start_first_whatever_ended_last()
    {
        await using var subject = await Create();
        await subject.Start(Id(1), _run, JobKey.From("a"), At(1));
        await subject.Start(Id(2), _run, JobKey.From("b"), At(2));
        await subject.Start(Id(3), _run, JobKey.From("c"), At(3));
        await subject.Finish(Id(1));

        var all = (await subject.Views.List(state: null, kind: null, Token)).Value;

        Expect.Sequence([Id(3), Id(2), Id(1)], all.Select(static r => r.Id));
    }

    /// <summary>Jobs that started at the same instant are ordered by id, in both lists.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Jobs_started_together_are_ordered_by_id()
    {
        await using var subject = await Create();
        await subject.Start(Id(2), _run, JobKey.From("b"), At(1));
        await subject.Start(Id(1), _run, JobKey.From("a"), At(1));

        Expect.Sequence([Id(1), Id(2)], (await subject.Views.Running(Token)).Value.Select(static r => r.Id));
        Expect.Sequence([Id(1), Id(2)], (await subject.Views.List(state: null, kind: null, Token)).Value.Select(static r => r.Id));
    }

    /// <summary>List filters by state, by kind, and by both at once.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task List_filters_by_state_and_kind_together()
    {
        await using var subject = await Create();
        await subject.Start(Id(1), _run, JobKey.From("a"), At(1));
        await subject.Start(Id(2), _sync, JobKey.From("b"), At(2));
        await subject.Start(Id(3), _run, JobKey.From("c"), At(3));
        await subject.Finish(Id(1));
        await subject.Finish(Id(2));

        Expect.Sequence([Id(3), Id(1)], (await subject.Views.List(state: null, _run, Token)).Value.Select(static r => r.Id));
        Expect.Sequence([Id(3)], (await subject.Views.List(JobState.Running, _run, Token)).Value.Select(static r => r.Id));
        Expect.Sequence([Id(1)], (await subject.Views.List(JobState.Succeeded, _run, Token)).Value.Select(static r => r.Id));
        Expect.Sequence([Id(2)], (await subject.Views.List(JobState.Succeeded, _sync, Token)).Value.Select(static r => r.Id));
        Expect.Equal(0, (await subject.Views.List(JobState.Running, _sync, Token)).Value.Count);
    }
}
