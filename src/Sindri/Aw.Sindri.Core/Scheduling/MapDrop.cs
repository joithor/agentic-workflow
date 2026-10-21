namespace Aw.Sindri.Core.Scheduling;

/// <summary>Release the hold without promoting.</summary>
/// <param name="Reason">Why.</param>
public sealed record MapDrop(MapDropReason Reason) : MapPublishAction;
