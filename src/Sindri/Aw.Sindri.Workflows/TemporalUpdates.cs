using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What every call on an instance's workflow shares (engine spec rev 8 §5.2, §5.3): an Update waits at most
/// <see cref="Patience"/> for a worker to take it, and a NotFound that names the namespace says the Temporal it reached
/// isn't serve's (SND-API-001), not that the instance is gone.
/// </summary>
internal static class TemporalUpdates
{
    /// <summary>
    /// How long an Update waits for a worker: 4 s, so a command that first waited the 5 s command gate still answers
    /// inside the CLI's 10 s HTTP timeout, and a serve without its worker fails cleanly instead of hanging.
    /// </summary>
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(4);

    /// <summary>The Update's RPC options: <see cref="Patience"/> and the caller's token.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The options.</returns>
    internal static RpcOptions Rpc(CancellationToken cancellationToken) => new() { CancellationToken = cancellationToken, Timeout = Patience };

    /// <summary>Whether <paramref name="e"/> is the frontend saying it has no such namespace.</summary>
    /// <param name="e">The RPC failure.</param>
    /// <returns>True for a NotFound that names the namespace as the missing thing, not a workflow execution inside it.</returns>
    internal static bool NoNamespace(RpcException e) =>
        e.Code == RpcException.StatusCode.NotFound && e.Message.Contains("namespace", StringComparison.OrdinalIgnoreCase)
        && !e.Message.Contains("execution not found", StringComparison.OrdinalIgnoreCase);   // a missing workflow's message names its namespace too ("WorkflowId{namespace='default', …}")

    /// <summary>SND-API-001 for a Temporal without the client's namespace.</summary>
    /// <param name="client">The client, on the settings' namespace.</param>
    /// <param name="e">The RPC failure.</param>
    /// <returns>The error.</returns>
    internal static Error NotServes(ITemporalClient client, RpcException e) =>
        SindriErrors.NotRunning.With($"Temporal at {client.Connection.Options.TargetHost} has no '{client.Options.Namespace}' namespace: {e.Message} "
            + "(fix: start sindri serve on this home, or set AW_SINDRI_TEMPORAL_NAMESPACE to the namespace serve uses)");

    /// <summary>SND-API-002 for an Update no worker took in <see cref="Patience"/>; it may still apply once one runs.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="nonce">The Update's id, for the retry.</param>
    /// <returns>The error.</returns>
    internal static Error NoWorker(InstanceAddress address, CommandNonce nonce) =>
        SindriErrors.ApiTimeout.With($"no worker took the command for {address.Value} within {Patience.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s; it may still apply once one runs "
            + $"(fix: start sindri serve, which runs the worker, then run the same command again with --nonce {nonce.Value})");
}
