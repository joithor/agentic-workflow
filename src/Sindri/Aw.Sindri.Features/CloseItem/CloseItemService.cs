using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.CloseItem;

/// <summary>
/// Closes an item as done or cancelled (API spec §3.2, §2.1; desk): resolves it through the views (a human sees every repo), decides on the
/// version it shows, and appends 'item.state-changed' keyed by the item and the version it moves to. Losing the race for that key to another
/// command is SND-ITEM-409; a replay of this same command is accepted again at the recorded position.
/// </summary>
/// <param name="items">Resolves the item by id or alias.</param>
/// <param name="ledger">Appends the state change and reads back a key that was taken.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry and reads a recorded payload.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class CloseItemService(IItemResolver items, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers)
    : IService<CloseItemRequest, CommandAccepted>
{
    public async ValueTask<Result<CommandAccepted>> Handle(CloseItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("CloseItem needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var resolved = await items.Resolve(request.Item, caller, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var decided = ItemDecisions.Close(resolved.Value, request.Reason, request.ItemVersion, caller.CommandId);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var change = decided.Value;
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ItemStateChanged}:{change.Item.Value}:v{change.Version}");
        var entry = events.Entry(EventTypes.ItemStateChanged, change, key, change.Item.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        if (appended.Value.AlreadyRecorded && await TakenByAnother(appended.Value.Sequence, caller.CommandId, cancellationToken).ConfigureAwait(false) is { } taken)
        {
            return taken;
        }

        return new CommandAccepted(caller.CommandId, appended.Value.Sequence);
    }

    /// <summary>The conflict when the event at <paramref name="sequence"/> belongs to a command other than <paramref name="mine"/>; null when it is ours (a replay).</summary>
    private async ValueTask<Error?> TakenByAnother(long sequence, CommandId mine, CancellationToken cancellationToken)
    {
        var read = await ledger.ReadAfter(sequence - 1, 1, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        if (read.Value.Count == 0 || read.Value[0].Sequence != sequence)
        {
            return SindriErrors.LedgerUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"the event at sequence {sequence} can't be read back"));
        }

        var payload = events.Payload<ItemStateChanged>(read.Value[0]);
        if (payload.IsError)
        {
            return payload.Error;
        }

        return payload.Value.CommandId == mine ? null : SindriErrors.ItemConflict.With($"{payload.Value.Item.Value} changed since you looked");
    }
}
