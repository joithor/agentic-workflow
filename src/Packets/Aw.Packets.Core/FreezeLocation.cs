using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

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
        return Of(record.Repo, record.Packet, record.Hash);
    }

    /// <summary>The location of the record for <paramref name="hash"/> of <paramref name="packet"/> in <paramref name="repo"/>.</summary>
    /// <param name="repo">The repository.</param>
    /// <param name="packet">The packet id.</param>
    /// <param name="hash">The canonical hash.</param>
    /// <returns>The relative path.</returns>
    public static string Of(RepoSlug repo, PacketId packet, PacketHash hash) =>
        $"{repo.Value}/{packet.Value}/frozen/{hash.Hex}.json";
}
