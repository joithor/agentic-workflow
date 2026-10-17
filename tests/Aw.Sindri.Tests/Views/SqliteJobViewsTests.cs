using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>The jobs view in views.db (API P6 unit B): rows the real projection folds are read back by id and as the running list; the list's order and filters are the JobViewsContract's.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteJobViewsTests : IDisposable
{
    private static readonly JobKind _run = JobKind.From("RunService");
    private static readonly JobKind _sync = JobKind.From("SyncSources");
    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();
    private readonly SqliteViews _views;
    private long _sequence;

    public SqliteJobViewsTests()
    {
        _views = new SqliteViews(Path.Combine(_home.Path, "home"));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SqliteJobViews Jobs => new(_views.Reader);

    public void Dispose()
    {
        _views.Dispose();
        _home.Dispose();
    }

    private static JobId Id(int n) => JobId.From("JOB-" + n.ToString("x32", CultureInfo.InvariantCulture));

    private async Task Apply<TPayload>(string type, TPayload payload)
    {
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        var applied = (await _views.Store.Apply([Recorded.Of(_h.Events, type, payload, ++_sequence)], [new JobsProjection(_h.Events)], _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(applied.Stall);
    }

    private Task Start(int n, JobKind kind, string key) =>
        Apply(EventTypes.JobStarted, new JobStarted(_h.Ids.New<CommandId>(), Id(n), kind, JobKey.From(key), Item: null, Repo: null, Service: null));

    private Task Finish(int n) =>
        Apply(EventTypes.JobFinished, new JobFinished(_h.Ids.New<CommandId>(), Id(n), JobOutcome.Succeeded, "done", Code: null, Result: null));

    [Fact]
    public async Task Jobs_view_reads_running_and_by_id()
    {
        await Start(1, _run, "heartbeat");
        await Start(2, _sync, "all");
        await Start(3, _run, "index");
        await Finish(1);

        var one = (await Jobs.Get(Id(1), Token)).Value;
        var missing = (await Jobs.Get(Id(9), Token)).Value;
        var running = (await Jobs.Running(Token)).Value;

        Expect.Equal(JobState.Succeeded, one!.State);
        Assert.Null(missing);
        Expect.Sequence([Id(2), Id(3)], running.Select(r => r.Id));
    }

    [Fact]
    public async Task A_missing_views_db_reads_as_no_jobs()
    {
        Assert.Null((await Jobs.Get(Id(1), Token)).Value);
        Assert.Empty((await Jobs.Running(Token)).Value);
        Assert.Empty((await Jobs.List(state: null, kind: null, Token)).Value);
    }
}
