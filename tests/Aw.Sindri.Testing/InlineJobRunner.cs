using System.Collections.Concurrent;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Testing;

/// <summary>
/// Runs a job's work to completion inside <see cref="Start"/>, for deterministic unit tests (API P6): the starting caller is re-entered, the job
/// context is set (its progress lines are collected in <see cref="Lines"/>), the work gets a DI scope of its own, and the result or the failure is
/// recorded in <see cref="Finished"/>. It writes no ledger events: the real runner's writer is covered by the JobRuntimeContract.
/// </summary>
/// <param name="scopes">Gives the work its scope.</param>
/// <param name="jobs">The accessor the work reads its context from.</param>
/// <param name="callers">The caller accessor the starting caller is re-entered through.</param>
public sealed class InlineJobRunner(IServiceScopeFactory scopes, IJobContextAccessor jobs, ICallerContextAccessor callers) : IJobRunner
{
    private readonly ConcurrentQueue<(JobId Job, string Line)> _lines = new();
    private readonly ConcurrentQueue<(JobId Job, JobWorkResult Result)> _finished = new();

    /// <summary>Every progress line the work reported, in order.</summary>
    public IReadOnlyList<(JobId Job, string Line)> Lines => [.. _lines];

    /// <summary>How each started job's work ended, in order. A throw is recorded as a failed result carrying the exception's message.</summary>
    public IReadOnlyList<(JobId Job, JobWorkResult Result)> Finished => [.. _finished];

    /// <inheritdoc />
    public int Running => 0;

    /// <inheritdoc />
    public async ValueTask Start(JobStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            using var caller = callers.Enter(start.Caller);
            using var job = jobs.Enter(new JobContext(start.Job, start.Kind, new Inline(line => _lines.Enqueue((start.Job, line)))));
            try
            {
                _finished.Enqueue((start.Job, await start.Definition.Run(scope.ServiceProvider, start.Request, cancellationToken).ConfigureAwait(false)));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _finished.Enqueue((start.Job, new JobWorkResult(Aw.Sindri.Core.SindriErrors.ServiceFailed.With(e.Message), string.Empty, Result: null)));
            }
        }
    }

    /// <inheritdoc />
    public bool Cancel(JobId job) => false;

    /// <summary>Reports on the calling thread, so the lines are there when Start returns (Progress&lt;T&gt; would post them to the pool).</summary>
    private sealed class Inline(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
