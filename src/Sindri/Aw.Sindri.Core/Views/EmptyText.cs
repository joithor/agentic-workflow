using System.Globalization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Core.Views;

/// <summary>The empty, loading and error sentences of the list views (API spec §3.6; P2 and P3): the CLI prints them and every list returns one in 'empty'.</summary>
public static class EmptyText
{
    /// <summary>The services list is empty.</summary>
    /// <returns>The sentence.</returns>
    public static string Services() => "No services are approved: run `sindri profile approve`.";

    /// <summary>The queue has no open item in any group.</summary>
    /// <returns>The sentence.</returns>
    public static string Queue() => "Nothing in the queue. Add one with `sindri item add`.";

    /// <summary>A queue group is empty (UI spec E-18): a sentence for every group, Parked included.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The sentence.</returns>
    public static string Group(QueueGroup group) => group switch
    {
        QueueGroup.WaitingOnYou => "Nothing is waiting on you.",
        QueueGroup.WithYou => "Nothing is with you.",
        QueueGroup.Parked => "Nothing parked.",
        QueueGroup.Running => "Nothing running.",
        QueueGroup.Queued => "Nothing queued.",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "not a queue group"),
    };

    /// <summary>The item list is empty for its filters.</summary>
    /// <param name="state">The state filter, or null.</param>
    /// <param name="repo">The repo filter, or null.</param>
    /// <returns>The sentence.</returns>
    public static string Items(ItemState? state, RepoName? repo)
    {
        var what = state is { } s ? "No " + Name(s) + " items" : "No items";
        return repo is { } r ? what + " in " + r.Value + "." : what + ".";
    }

    /// <summary>An item's timeline has no line yet.</summary>
    /// <returns>The sentence.</returns>
    public static string Timeline() => "Nothing has happened to this item yet.";

    /// <summary>The views are rebuilding.</summary>
    /// <param name="cursor">The projector's cursor.</param>
    /// <param name="head">The ledger head.</param>
    /// <returns>The sentence.</returns>
    public static string Rebuilding(long cursor, long head) =>
        string.Create(CultureInfo.InvariantCulture, $"Rebuilding views (cursor {cursor} of {head})…");

    /// <summary>The projector stalled.</summary>
    /// <param name="at">The sequence it stalled at.</param>
    /// <returns>The sentence.</returns>
    public static string Stalled(long at) =>
        string.Create(CultureInfo.InvariantCulture, $"Views are stalled at {at}: run `sindri views rebuild`");

    /// <summary>The sentence a list answers with: stalled, then rebuilding, then its empty sentence when it has no rows, else null.</summary>
    /// <param name="asOf">The answer's freshness.</param>
    /// <param name="empty">True when the list has no rows.</param>
    /// <param name="emptySentence">The list's empty sentence.</param>
    /// <returns>The sentence, or null.</returns>
    public static string? For(AsOf asOf, bool empty, Func<string> emptySentence)
    {
        ArgumentNullException.ThrowIfNull(asOf);
        ArgumentNullException.ThrowIfNull(emptySentence);
        return asOf.Stalled ? Stalled(asOf.Cursor + 1)
            : asOf.Rebuilding ? Rebuilding(asOf.Cursor, asOf.LedgerHead)
            : empty ? emptySentence() : null;
    }

    private static string Name(ItemState state) => state switch
    {
        ItemState.New => "new",
        ItemState.Queued => "queued",
        ItemState.Running => "running",
        ItemState.NeedsDecision => "needs-decision",
        ItemState.NeedsAnswer => "needs-answer",
        ItemState.Parked => "parked",
        ItemState.TakenOver => "taken-over",
        ItemState.Done => "done",
        ItemState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not an item state"),
    };
}
