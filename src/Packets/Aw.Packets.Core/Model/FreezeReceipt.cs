namespace Aw.Packets.Core.Model;

/// <summary>Where a freeze is recorded.</summary>
/// <param name="Location">The record's path relative to the packets/ namespace: '&lt;packet&gt;/frozen/&lt;hex&gt;.json'.</param>
/// <param name="AlreadyFrozen">True when the same packet and hash were recorded before (nothing was written).</param>
public sealed record FreezeReceipt(string Location, bool AlreadyFrozen);
