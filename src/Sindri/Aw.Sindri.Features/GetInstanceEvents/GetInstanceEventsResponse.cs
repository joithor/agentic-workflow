namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>The instance's events, oldest first, and the last ledger sequence read (the cursor '--follow' reads after next).</summary>
/// <param name="Events">The events.</param>
/// <param name="Cursor">The last sequence read, whether or not it was the instance's.</param>
public sealed record GetInstanceEventsResponse(IReadOnlyList<InstanceEventView> Events, long Cursor);
