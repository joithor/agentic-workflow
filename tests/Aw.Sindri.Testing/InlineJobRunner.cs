using System.Collections.Concurrent;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Testing;

/// <summary>
/// Runs a job's work to completion inside <see cref="Start"/>, for deterministic unit tests (API P6): the starting caller (or, for a service's job,
/// 'service:&lt;name&gt;') is entered, the job context is set (its progress lines are collected in <see cref="Lines"/>), the work gets a DI scope of its
/// own, and the result or the failure is recorded in <see cref="Finished"/>. A cancel while the work runs ends it with no result, as the real runner
/// does, and <see cref="Running"/> counts the jobs inside <see cref="Start"/>. It writes no ledger events: the real runner's writer is covered by the
/// JobRuntimeContract; this fake is held to the real runner by the JobRunnerContract.
/// </summary>
/// <param name="scopes">Gives the work its scope.</param>
/// <param name="jobs">The accessor the work reads its context from.</param>
/// <param name="callers">The caller accessor the starting caller is re-entered through.</param>
public sealed class InlineJobRunner(IServiceScopeFactory scopes, IJobContextAccessor jobs, ICallerContextAccessor callers) : IJobRunner
{
    private readonly ConcurrentQueue<(JobId Job, string Line)> _lines = new();
    private readonly ConcurrentQueue<(JobId Job, JobWorkResult Result)> _finished = new();
    private readonly ConcurrentDictionary<string, Run> _runs = new(StringComparer.Ordinal);

    /// <summary>Every progress line the work reported, in order.</summary>
    public IReadOnlyList<(JobId Job, string Line)> Lines => [.. _lines];

    /// <summary>How each started job's work ended, in order. A throw is recorded as a failed result carrying the exception's message; a cancelled job records none.</summary>
    public IReadOnlyList<(JobId Job, JobWorkResult Result)> Finished => [.. _finished];

    /// <inheritdoc />
    public int Running => _runs.Count;

    /// <inheritdoc />
    public async ValueTask Start(JobStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        using var run = new Run();
        _runs[start.Job.Value] = run;
        try
        {
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                using var caller = start.Service is { } service ? callers.EnterService(service.Value) : callers.Enter(start.Caller);
                using var job = jobs.Enter(new JobContext(start.Job, start.Kind, new Inline(line => _lines.Enqueue((start.Job, line)))));
                _finished.Enqueue((start.Job, await start.Definition.Run(scope.ServiceProvider.GetRequiredService, start.Request, run.Source.Token).ConfigureAwait(false)));
            }
        }
        catch (OperationCanceledException) when (run.Source.IsCancellationRequested || run.Cancelled)
        {
            // cancelled: nothing to record, CancelJob wrote the end
        }
#pragma warning disable CA1031 // boundary: whatever the work throws is the job's failure, as in the real runner
        catch (Exception e)
#pragma warning restore CA1031
        {
            _finished.Enqueue((start.Job, new JobWorkResult(Core.SindriErrors.ServiceFailed.With(e.Message), string.Empty, Result: null)));
        }
        finally
        {
            _ = _runs.TryRemove(start.Job.Value, out _);
        }
    }

    /// <inheritdoc />
    public bool Cancel(JobId job)
    {
        if (!_runs.TryGetValue(job.Value, out var run))
        {
            return false;
        }

        run.Cancelled = true;
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

    /// <inheritdoc />
    public void Cancelling(JobId job)
    {
        if (_runs.TryGetValue(job.Value, out var run))
        {
            run.Cancelled = true;
        }
    }

    /// <inheritdoc />
    public void Withdraw(JobId job)
    {
        if (_runs.TryGetValue(job.Value, out var run))
        {
            run.Cancelled = false;
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> CancelRecorded(JobId job, CancellationToken cancellationToken) => ValueTask.FromResult(IsCancelling(job));   // the inline runner has no append in flight: a mark is a record

    /// <inheritdoc />
    public bool IsCancelling(JobId job) => _runs.TryGetValue(job.Value, out var run) && run.Cancelled;

    /// <summary>Reports on the calling thread, so the lines are there when Start returns (Progress&lt;T&gt; would post them to the pool).</summary>
    private sealed class Inline(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    private sealed class Run : IDisposable
    {
        public CancellationTokenSource Source { get; } = new();

        private volatile bool _cancelled;

        public bool Cancelled
        {
            get => _cancelled;
            set => _cancelled = value;
        }

        public void Dispose() => Source.Dispose();
    }
}
