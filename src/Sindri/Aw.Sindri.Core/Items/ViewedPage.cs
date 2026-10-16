using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Items;

/// <summary>A page the caller may see.</summary>
/// <param name="Events">The events, oldest first.</param>
/// <param name="ScannedTo">The last sequence the read looked at, shown or not.</param>
public sealed record ViewedPage(IReadOnlyList<PublicEvent> Events, long ScannedTo);
