using System.Collections.Concurrent;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The process-local runner (API P6 unit C): one task per job outside the command gate, with a writer for its progress lines and its end. The work runs
/// under the starting caller (plan 1.4 (d)) and a token linked from the job's own source (tripped by CancelJob) and the host's stop. A cancelled job
/// writes no end of its own (CancelJob appended 'job.cancelled'); a job the host's stop cancelled writes none either, recovery ends it at the next start.
/// It takes no heavy-job slot: a job that needs one (the context map build) takes it itself.
/// </summary>
/// <param name="openScope">Opens a DI scope for each job's work.</param>
/// <param name="jobs">Where the work reads its job context.</param>
/// <param name="callers">The starting caller is re-entered for the work.</param>
/// <param name="writer">Appends the job's own events.</param>
/// <param name="registry">Releases a job's (kind, key) claim when it ends.</param>
internal sealed class JobRunner(Func<JobScope> openScope, IJobContextAccessor jobs, ICallerContextAccessor callers, JobEventWriter writer, JobRegistry registry)
    : IJobRunner, IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, Run> _runs = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();
    private int _disposed;

    /// <inheritdoc />
    public int Running => _runs.Count;

    /// <inheritdoc />
    public ValueTask Start(JobStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var run = new Run(start, CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token));
        _runs[start.Job.Value] = run;
        using (ExecutionContext.SuppressFlow())   // the job's task starts with no ambient caller or job of the code that started it
        {
            _ = Task.Run(() => Execute(run), CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public bool Cancel(JobId job)
    {
        if (!_runs.TryGetValue(job.Value, out var run))
        {
            return false;
        }

        run.Cancelled = true;
        // the (kind, key) claim stays until the work has returned (Execute releases it): a service whose old run is still winding down must never overlap a new one
        try
        {
            run.Source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return false;   // it ended just now
        }

        return true;
    }

    /// <summary>Completes when every job started so far has ended and its last event is appended.</summary>
    /// <returns>A task that completes then.</returns>
    public async Task Idle()
    {
        while (true)
        {
            var pending = _runs.Values.Select(static r => r.Done.Task).ToArray();
            if (pending.Length == 0)
            {
                return;
            }

            await Task.WhenAll(pending).ConfigureAwait(false);
        }
    }

    /// <summary>Trips every running job's token (the host is stopping) without waiting; a container disposed synchronously calls this.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _stopping.Cancel();
        }
    }

    /// <summary>Trips every running job's token (the host is stopping) and waits for their tasks.</summary>
    /// <returns>A task that completes then.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;   // registered under both its type and IJobRunner: the container disposes it twice
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        await Idle().ConfigureAwait(false);
        _stopping.Dispose();
    }

    private async Task Execute(Run run)
    {
        var queue = new JobLineQueue(JobLimits.QueueLines);
        var drained = Task.Run(() => Drain(run, queue), CancellationToken.None);
        var work = await Work(run, queue).ConfigureAwait(false);
        queue.Complete();
        await drained.ConfigureAwait(false);
        try
        {
            if (work is not null && !run.Cancelled && !_stopping.IsCancellationRequested)
            {
                _ = await writer.Finish(run.Start, work, CancellationToken.None).ConfigureAwait(false);   // a lost write is recovery's to end at the next start
            }
        }
        finally
        {
            registry.Release(run.Start.Job);
            _ = _runs.TryRemove(run.Start.Job.Value, out _);
            run.Source.Dispose();
            _ = run.Done.TrySetResult();
        }
    }

    /// <summary>Runs the work under the starting caller; null when it was cancelled (nothing to record), otherwise how it ended.</summary>
    private async Task<JobWorkResult?> Work(Run run, JobLineQueue queue)
    {
        var start = run.Start;
        try
        {
            var scope = openScope();
            await using (scope.Lifetime.ConfigureAwait(false))
            {
                using var caller = callers.Enter(start.Caller);
                using var job = jobs.Enter(new JobContext(start.Job, start.Kind, new QueueProgress(queue)));
                return await start.Definition.Run(scope.Resolve, start.Request, run.Source.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (run.Source.IsCancellationRequested)
        {
            return null;
        }
#pragma warning disable CA1031 // boundary: whatever the work throws becomes the job's failure, not the process's
        catch (Exception e)
#pragma warning restore CA1031
        {
            return new JobWorkResult(SindriErrors.ServiceFailed.With(e.Message), string.Empty, Result: null);
        }
    }

    /// <summary>Writes the queued lines as 'job.progress' within the budget; a write that fails (no lease) stops the writing and cancels the job.</summary>
    private async Task Drain(Run run, JobLineQueue queue)
    {
        var budget = new JobProgressBudget();
        var broken = false;
        await foreach (var line in queue.ReadAll(CancellationToken.None).ConfigureAwait(false))
        {
            if (broken)
            {
                continue;
            }

            var admitted = budget.Admit();
            if (admitted == JobAdmission.Drop)
            {
                continue;
            }

            var text = admitted == JobAdmission.Truncate ? "(progress truncated)" : line;
            if ((await writer.Progress(run.Start, budget.Written, text, CancellationToken.None).ConfigureAwait(false)).IsError)
            {
                broken = true;
                run.Cancelled = true;   // a lost lease ends the job here; recovery writes its end
                await run.Source.CancelAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class QueueProgress(JobLineQueue queue) : IProgress<string>
    {
        public void Report(string value) => queue.Offer(value);
    }

    private sealed class Run(JobStart start, CancellationTokenSource source)
    {
        public JobStart Start { get; } = start;

        public CancellationTokenSource Source { get; } = source;

        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Cancelled { get; set; }
    }
}
