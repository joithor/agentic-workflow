#pragma warning disable MA0182 // red stub: used once unit C is implemented
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Jobs;

/// <summary>The process-local runner (API P6 unit C): one task per job outside the command gate, a writer for its progress and its end. It takes no heavy-job slot.</summary>
internal sealed class JobRunner : IJobRunner, IAsyncDisposable
{
    /// <summary>Makes the runner.</summary>
    /// <param name="scopes">Gives each job's work a DI scope.</param>
    /// <param name="jobs">Where the work reads its job context.</param>
    /// <param name="callers">The starting caller is re-entered for the work; the writer enters 'service:jobs'.</param>
    /// <param name="writer">Appends the job's own events.</param>
    /// <param name="registry">Releases a job's (kind, key) claim when it ends.</param>
    public JobRunner(IServiceScopeFactory scopes, IJobContextAccessor jobs, ICallerContextAccessor callers, JobEventWriter writer, JobRegistry registry)
    {
        _ = (scopes, jobs, callers, writer, registry);
    }

    /// <inheritdoc />
    public int Running => throw new NotSupportedException("red until API P6 unit C");

    /// <inheritdoc />
    public ValueTask Start(JobStart start, CancellationToken cancellationToken) => throw new NotSupportedException("red until API P6 unit C");

    /// <inheritdoc />
    public bool Cancel(JobId job) => throw new NotSupportedException("red until API P6 unit C");

    /// <summary>Completes when every job started so far has ended and its last event is appended.</summary>
    /// <returns>A task that completes then.</returns>
    public Task Idle() => throw new NotSupportedException("red until API P6 unit C");

    /// <summary>Trips every running job's token (the host is stopping) and waits for their tasks.</summary>
    /// <returns>A task that completes then.</returns>
    public ValueTask DisposeAsync() => throw new NotSupportedException("red until API P6 unit C");
}
