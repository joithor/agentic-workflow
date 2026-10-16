namespace Aw.Sindri.Ui.Api;

/// <summary>One group of GET /sindri/queue (API spec §3.6, UI spec E-18): its name, its items in the API's order, and its own EmptyText sentence.</summary>
/// <param name="Group">The group's name ('waitingOnYou', 'withYou', 'running', 'queued').</param>
/// <param name="Items">Its items, oldest first, as the API orders them.</param>
/// <param name="Empty">The group's sentence when it has no items ("Nothing running."), else null.</param>
internal sealed record QueueGroupView(string Group, IReadOnlyList<ItemSummaryView> Items, string? Empty);
