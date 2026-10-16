using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The in-memory jobs view as a view subject (API P6 review T9).</summary>
internal sealed class InMemoryJobViewsSubject : IJobViewsSubject
{
    private readonly InMemoryJobViews _views = new();
    private readonly Dictionary<JobId, JobRow> _rows = [];
    private long _sequence;

    public IJobViews Views => _views;

    public Task Start(JobId job, JobKind kind, JobKey key, DateTimeOffset at)
    {
        var row = new JobBuilder().WithId(job).OfKind(kind, key).Build() with { StartedAt = at, Sequence = ++_sequence };
        _rows[job] = row;
        _views.Put(row);
        return Task.CompletedTask;
    }

    public Task Finish(JobId job)
    {
        var row = _rows[job] with { State = JobState.Succeeded, Summary = "done", Sequence = ++_sequence };
        row = row with { EndedAt = row.StartedAt.AddMinutes(1) };
        _rows[job] = row;
        _views.Put(row);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
