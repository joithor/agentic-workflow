using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>The 'jobs' view read through the views.db reader (API P6 unit B): one job by id, the running ones, and the list by state and kind.</summary>
/// <param name="reader">The views.db read side.</param>
internal sealed class SqliteJobViews(IViewReader reader) : IJobViews
{
    public ValueTask<Result<JobRow?>> Get(JobId job, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red until API P6 unit B {reader}");

    public ValueTask<Result<IReadOnlyList<JobRow>>> Running(CancellationToken cancellationToken) => throw new NotSupportedException("red until API P6 unit B");

    public ValueTask<Result<IReadOnlyList<JobRow>>> List(JobState? state, JobKind? kind, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit B");
}
