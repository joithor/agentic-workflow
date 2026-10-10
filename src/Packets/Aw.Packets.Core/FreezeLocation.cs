using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>
/// Where a freeze record lives, relative to the packets/ namespace: '&lt;owner&gt;/&lt;name&gt;/&lt;packet&gt;/frozen/&lt;hex&gt;.json'.
/// Per repository, because a packet id is unique only within one.
/// </summary>
public static class FreezeLocation
{
    /// <summary>The location of <paramref name="record"/>.</summary>
    /// <param name="record">The record.</param>
    /// <returns>The relative path.</returns>
    public static string Of(FreezeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return $"{record.Repo.Value}/{record.Packet.Value}/frozen/{record.Hash.Hex}.json";
    }
}
