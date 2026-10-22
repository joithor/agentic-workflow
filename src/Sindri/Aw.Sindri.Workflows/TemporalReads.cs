using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The bound on a control-plane read (Query, Describe, List): the SDK retries a call to a frontend that is gone for about
/// 5 s, which a CLI read has no use for. A read here turns the SDK's retry off for its own RPCs and tries the frontend again a
/// few times itself, so Unavailable surfaces in about a second and maps to the same errors as before. Per call, not per
/// connection: writes, signals, Updates, the worker's long polls and its heartbeats keep the SDK's default retry.
/// </summary>
internal static class TemporalReads
{
    /// <summary>The pauses between a read's attempts (three attempts, about 0.6 s of waiting).</summary>
    internal static readonly IReadOnlyList<TimeSpan> Pauses = [TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400)];

    /// <summary>The RPC options of a read: no SDK retry, an optional per-attempt timeout.</summary>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <param name="timeout">The per-attempt timeout, or null for the call's default.</param>
    /// <returns>The options.</returns>
    internal static RpcOptions Rpc(CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        new() { Retry = false, Timeout = timeout, CancellationToken = cancellationToken };

    /// <summary>Runs <paramref name="read"/>, again after a pause while the frontend answers with a transient code (the codes the SDK retries, but never DeadlineExceeded: a Query's own patience is one attempt); the last failure is thrown.</summary>
    /// <typeparam name="T">What the read returns.</typeparam>
    /// <param name="read">The read, from the RPC options to use; it must be safe to run again from the start.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <param name="timeout">The per-attempt deadline of each RPC, or null for the call's default.</param>
    /// <returns>What the read returned.</returns>
    internal static async Task<T> Run<T>(Func<RpcOptions, Task<T>> read, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await read(Rpc(cancellationToken, timeout)).ConfigureAwait(false);
            }
            catch (RpcException e) when (Transient(e.Code) && attempt < Pauses.Count) // boundary: the frontend may be coming back
            {
                await Task.Delay(Pauses[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool Transient(RpcException.StatusCode code) => code is RpcException.StatusCode.Unavailable or RpcException.StatusCode.Internal
        or RpcException.StatusCode.Unknown or RpcException.StatusCode.Aborted or RpcException.StatusCode.ResourceExhausted or RpcException.StatusCode.DataLoss;

    /// <summary>The first execution of a visibility query that <paramref name="match"/> accepts (the listing stops paging there), or null.</summary>
    /// <param name="client">The client.</param>
    /// <param name="query">The visibility query.</param>
    /// <param name="match">Which execution is wanted.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <param name="timeout">The per-attempt deadline of each RPC, or null for the call's default.</param>
    /// <returns>The execution, or null when none matches.</returns>
    internal static Task<WorkflowExecution?> Find(ITemporalClient client, string query, Func<WorkflowExecution, bool> match, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(match);
        return Run(async rpc =>
        {
            await foreach (var execution in client.ListWorkflowsAsync(query, new WorkflowListOptions { Rpc = rpc }).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (match(execution))
                {
                    return execution;
                }
            }

            return null;
        }, cancellationToken, timeout);
    }

    /// <summary>The executions of a visibility query, listed whole by <see cref="Run{T}"/>.</summary>
    /// <param name="client">The client.</param>
    /// <param name="query">The visibility query.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <param name="timeout">The per-attempt deadline of each RPC, or null for the call's default.</param>
    /// <returns>Each execution's id and start time with its search attributes, as the caller reads them.</returns>
    internal static Task<List<WorkflowExecution>> List(ITemporalClient client, string query, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        return Run(async rpc =>
        {
            var found = new List<WorkflowExecution>();
            await foreach (var execution in client.ListWorkflowsAsync(query, new WorkflowListOptions { Rpc = rpc }).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                found.Add(execution);
            }

            return found;
        }, cancellationToken, timeout);
    }
}
