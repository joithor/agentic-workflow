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

    /// <summary>
    /// The Update's wait: one RPC bounded by <see cref="Patience"/> in total. The per-RPC timeout alone is not a bound: the
    /// client core retries an RPC that timed out, so a serve without a worker took 8 s and sometimes 12 s to answer, past the
    /// CLI's 10 s HTTP timeout. The linked token is cancelled after <see cref="Patience"/>, whatever the retries did.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait (the caller's).</param>
    /// <returns>The options to send with the Update; dispose it when the call is over.</returns>
    internal static UpdateWait Wait(CancellationToken cancellationToken) => new(cancellationToken);

    /// <summary>The Update's RPC options with their total bound; see <see cref="Wait"/>.</summary>
    internal sealed class UpdateWait : IDisposable
    {
        private readonly CancellationTokenSource _patience;

        internal UpdateWait(CancellationToken cancellationToken)
        {
            _patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _patience.CancelAfter(Patience);
            Options = new RpcOptions { CancellationToken = _patience.Token, Timeout = Patience };
        }

        /// <summary>The RPC options: the caller's token plus <see cref="Patience"/> in total, and <see cref="Patience"/> for one attempt.</summary>
        internal RpcOptions Options { get; }

        public void Dispose() => _patience.Dispose();
    }

    /// <summary>Whether <paramref name="e"/> is the frontend saying it has no such namespace.</summary>
    /// <param name="e">The RPC failure.</param>
    /// <returns>True for a NotFound that names the namespace.</returns>
    internal static bool NoNamespace(RpcException e) =>
        e.Code == RpcException.StatusCode.NotFound && e.Message.Contains("namespace", StringComparison.OrdinalIgnoreCase);

    /// <summary>SND-API-001 for a Temporal without the client's namespace.</summary>
    /// <param name="client">The client, on the settings' namespace.</param>
    /// <param name="e">The RPC failure.</param>
    /// <returns>The error.</returns>
    internal static Error NotServes(ITemporalClient client, RpcException e) =>
        SindriErrors.NotRunning.With($"Temporal at {client.Connection.Options.TargetHost} has no '{client.Options.Namespace}' namespace: {e.Message} "
            + "(fix: start sindri serve on this home, or set AW_SINDRI_TEMPORAL_NAMESPACE to the namespace serve uses)");

    /// <summary>
    /// SND-API-001 for a frontend that went away under a live client (the dev server under serve died; serve itself answered):
    /// Unavailable is the transport saying so. A call that carries a nonce says what to check and how to resend it, because the
    /// Update may have reached the server before it went; a start or a signal has no nonce.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="e">The RPC failure.</param>
    /// <param name="nonce">The Update's id when the call has one: a retry with it resends the same Update id.</param>
    /// <returns>The error; its kind (Unavailable) keeps a fresh nonce claim.</returns>
    internal static Error Down(ITemporalClient client, RpcException e, CommandNonce? nonce = null) =>
        SindriErrors.NotRunning.With($"Temporal at {client.Connection.Options.TargetHost} doesn't answer: {e.Message} ("
            + (nonce is { } n
                ? $"fix: check `sindri workflow status` or `sindri workflow events` first; if the command didn't apply, run the same command again with --nonce {n.Value}; "
                : "fix: ")
            + "the Temporal dev server under serve is down, see `sindri doctor`)");

    /// <summary>SND-API-002 for an Update no worker took in <see cref="Patience"/>; it may still apply once one runs.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="nonce">The Update's id, for the retry.</param>
    /// <returns>The error.</returns>
    internal static Error NoWorker(InstanceAddress address, CommandNonce nonce) =>
        SindriErrors.ApiTimeout.With($"no worker took the command for {address.Value} within {Patience.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s; it may still apply once one runs "
            + $"(fix: start sindri serve, which runs the worker, then run the same command again with --nonce {nonce.Value})");
}
