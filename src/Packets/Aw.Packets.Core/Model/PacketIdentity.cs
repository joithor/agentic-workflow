using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Model;

/// <summary>Which packet a freeze record belongs to: the repository and the packet id (a record is kept per repository).</summary>
/// <param name="Repo">The repository.</param>
/// <param name="Packet">The packet id.</param>
public sealed record PacketIdentity(RepoSlug Repo, PacketId Packet);
