namespace Aw.Sindri.Core.Items;

/// <summary>The one place the queue's group set lives (API spec §3.6; owner decision 2026-10-12: four groups, parked items wait on you).</summary>
public static class QueueGroups
{
    /// <summary>The groups the queue shows, in order.</summary>
    public static IReadOnlyList<QueueGroup> Order { get; } = [QueueGroup.WaitingOnYou, QueueGroup.WithYou, QueueGroup.Running, QueueGroup.Queued];

    /// <summary>The group an item in <paramref name="state"/> shows in, or null when it is closed and not in the queue.</summary>
    /// <param name="state">The item's state.</param>
    /// <returns>The group, or null.</returns>
    public static QueueGroup? GroupOf(ItemState state) => state switch
    {
        ItemState.NeedsDecision or ItemState.NeedsAnswer or ItemState.Parked => QueueGroup.WaitingOnYou,
        ItemState.TakenOver => QueueGroup.WithYou,
        ItemState.Running => QueueGroup.Running,
        ItemState.New or ItemState.Queued => QueueGroup.Queued,
        ItemState.Done or ItemState.Cancelled => null,
        _ => null,
    };
}
