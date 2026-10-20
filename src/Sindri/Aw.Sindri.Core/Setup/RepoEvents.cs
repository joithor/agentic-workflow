using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Setup;

/// <summary>Records 'repo.added' (setup-once spec §5.1, §7.5): the repo's name and path, fenced by the lease this process holds, carrying the running command's id.</summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller whose command id the event carries.</param>
public sealed class RepoEvents(HeldLease held, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers)
{
    /// <summary>Whether an event can be recorded now: this process holds the lease and a call is running. A job checks it before it changes anything.</summary>
    public bool CanRecord() => held.Current is not null && callers.Current is not null;

    /// <summary>Appends 'repo.added' under <paramref name="key"/>.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="path">Its checkout (kept in the non-public payload).</param>
    /// <param name="key">The idempotency key: 'repo.added:&lt;commandId&gt;' for an add, 'repo.added:backfill:&lt;name&gt;' for a backfill.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or SND-API-001 when this process holds no lease or no call is running, or the ledger's error.</returns>
    public async ValueTask<Result<Unit>> Added(RepoName repo, string path, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(key);
        if (held.Current is not { } grant || callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await ledger.Append(events.Entry(EventTypes.RepoAdded, new RepoAdded(caller.CommandId, repo, path), key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }

    /// <summary>The key of an add made by the command running now.</summary>
    /// <returns>'repo.added:&lt;commandId&gt;', or null when no call is running.</returns>
    public string? KeyOfThisCommand() => callers.Current is { } caller ? "repo.added:" + caller.CommandId.Value : null;
}
