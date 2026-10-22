using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Setup;

/// <summary>Records 'mcp.registered' (setup-once spec §7.5) for the command in force: the repo, the port the registered address names and each provider's outcome. Key 'mcp.registered:&lt;commandId&gt;'.</summary>
/// <param name="held">The lease the control plane holds.</param>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller of the command in force.</param>
public sealed class McpEvents(HeldLease held, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers)
{
    /// <summary>Appends 'mcp.registered'.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="port">The port.</param>
    /// <param name="providers">One outcome per provider.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Unit, or why it could not be recorded.</returns>
    public async ValueTask<Result<Unit>> Registered(RepoName repo, int port, IReadOnlyList<McpProviderOutcome> providers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providers);
        if (held.Current is not { } grant || callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var key = "mcp.registered:" + caller.CommandId.Value;
        var appended = await ledger.Append(events.Entry(EventTypes.McpRegistered, new McpRegistered(caller.CommandId, repo, port, providers), key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
