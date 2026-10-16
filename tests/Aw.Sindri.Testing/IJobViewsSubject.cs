using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IJobViews"/> under test (API P6 review T9) and the two ways its rows come about.</summary>
public interface IJobViewsSubject : IAsyncDisposable
{
    /// <summary>The view.</summary>
    public IJobViews Views { get; }

    /// <summary>A job starts: its row appears running, started at <paramref name="at"/>.</summary>
    /// <param name="job">The job.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="key">Its key.</param>
    /// <param name="at">When it started.</param>
    /// <returns>A task that completes when the row is readable.</returns>
    public Task Start(JobId job, JobKind kind, JobKey key, DateTimeOffset at);

    /// <summary>The job ends as succeeded (later than any start so far): its row changes, its start time does not.</summary>
    /// <param name="job">The job.</param>
    /// <returns>A task that completes when the change is readable.</returns>
    public Task Finish(JobId job);
}
