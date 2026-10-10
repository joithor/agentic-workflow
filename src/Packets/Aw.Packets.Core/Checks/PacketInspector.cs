using Aw.Kernel;
using Aw.Packets.Core.Ports;

namespace Aw.Packets.Core.Checks;

/// <summary>Loads a packet and runs the deterministic checks (addendum §17.4) against its base commit.</summary>
/// <param name="source">Reads the packet directory.</param>
/// <param name="repo">Lists the repository's files at the base commit.</param>
public sealed class PacketInspector(IPacketSource source, IRepoSnapshot repo)
{
    /// <summary>Loads and checks the packet in <paramref name="packetDirectory"/>.</summary>
    /// <param name="packetDirectory">The packet directory.</param>
    /// <param name="repoDirectory">A directory inside the repository.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The inspection, or the load or repository error.</returns>
    public ValueTask<Result<Inspection>> Inspect(string packetDirectory, string repoDirectory, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"PacketInspector over {source} and {repo}: make ValidatePacketTests pass");
}
