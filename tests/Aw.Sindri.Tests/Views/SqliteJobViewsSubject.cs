using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>The SQLite jobs view as a view subject (API P6 review T9): rows come from the real projection folding 'job.started' and 'job.finished'.</summary>
internal sealed class SqliteJobViewsSubject : IJobViewsSubject
{
    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();
    private readonly SqliteViews _views;
    private long _sequence;

    public SqliteJobViewsSubject()
    {
        _views = new SqliteViews(Path.Combine(_home.Path, "home"));
        Views = new SqliteJobViews(_views.Reader);
    }

    public IJobViews Views { get; }

    public async Task Start(JobId job, JobKind kind, JobKey key, DateTimeOffset at)
    {
        _h.Clock.SetUtcNow(at);
        await Apply(EventTypes.JobStarted, new JobStarted(_h.Ids.New<CommandId>(), job, kind, key, Item: null, Repo: null, Service: null));
    }

    public async Task Finish(JobId job)
    {
        _h.Clock.Advance(TimeSpan.FromHours(1));   // after every start so far: the end must not move the job in the order
        await Apply(EventTypes.JobFinished, new JobFinished(_h.Ids.New<CommandId>(), job, JobOutcome.Succeeded, "done", Code: null, Result: null));
    }

    public ValueTask DisposeAsync()
    {
        _views.Dispose();
        _home.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task Apply<TPayload>(string type, TPayload payload)
    {
        var token = TestContext.Current.CancellationToken;
        _ = (await _views.Store.Open(ViewSchema.Version, token)).Value;
        var applied = (await _views.Store.Apply([Recorded.Of(_h.Events, type, payload, ++_sequence)], [new JobsProjection(_h.Events)], _h.Clock.GetUtcNow(), token)).Value;
        Assert.Null(applied.Stall);
    }
}
