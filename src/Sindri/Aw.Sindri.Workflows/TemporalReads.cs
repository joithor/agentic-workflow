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

    /// <summary>Runs <paramref name="read"/>, again after a pause while the frontend answers Unavailable; the last Unavailable is thrown.</summary>
    /// <typeparam name="T">What the read returns.</typeparam>
    /// <param name="read">The read, from the RPC options to use; it must be safe to run again from the start.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>What the read returned.</returns>
    internal static async Task<T> Run<T>(Func<RpcOptions, Task<T>> read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await read(Rpc(cancellationToken)).ConfigureAwait(false);
            }
            catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable && attempt < Pauses.Count) // boundary: the frontend may be coming back
            {
                await Task.Delay(Pauses[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The executions of a visibility query, listed whole by <see cref="Run{T}"/>.</summary>
    /// <param name="client">The client.</param>
    /// <param name="query">The visibility query.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>Each execution's id and start time with its search attributes, as the caller reads them.</returns>
    internal static Task<List<WorkflowExecution>> List(ITemporalClient client, string query, CancellationToken cancellationToken)
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
        }, cancellationToken);
    }
}
