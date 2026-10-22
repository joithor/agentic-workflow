namespace Aw.Sindri.Core.Scheduling;

/// <summary>Keep holding.</summary>
/// <param name="Until">When the wait ends (the hold's clamped start plus the wait).</param>
/// <param name="WaitingOn">How many repos are not yet built (and not absent).</param>
public sealed record MapWait(DateTimeOffset Until, int WaitingOn) : MapPublishAction;
