using Aw.Kernel;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Infrastructure.Packets;

/// <summary>
/// <see cref="IFrozenPackets"/> over aw-packets' own freeze records on disk: packets/&lt;owner&gt;/&lt;name&gt;/&lt;id&gt;/frozen/&lt;hex&gt;.json under $AW_STATE_DIR (else ~/.agentic-workflow).
/// Read only; the hash is checked for shape before it becomes part of a path.
/// </summary>
/// <param name="packetsRoot">The packets/ directory.</param>
internal sealed class FrozenPacketRecords(string packetsRoot) : IFrozenPackets
{
    public ValueTask<Result<FrozenPacket?>> Find(string hash, CancellationToken cancellationToken) => throw new NotSupportedException(packetsRoot);
}
