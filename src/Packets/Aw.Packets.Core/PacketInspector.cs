using Aw.Kernel;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>Loads a packet and runs the deterministic checks (addendum §17.4) against its base commit.</summary>
/// <param name="source">Reads the packet directory.</param>
/// <param name="repo">Lists the repository's files at the base commit.</param>
public sealed class PacketInspector(IPacketSource source, IRepoSnapshot repo)
{
    /// <summary>Loads and checks the packet in <paramref name="packetDirectory"/>.</summary>
    /// <param name="packetDirectory">The packet directory.</param>
    /// <param name="repoDirectory">A directory inside the repository.</param>
    /// <param name="sizes">The sizes file for PKT-SIZE-001; null leaves the rule unchecked.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The inspection; the load error; or AW-PACKETS-006 when the repository can't be read. A base commit
    /// missing from the repository is a PKT-BASE-001 failure, not an error.</returns>
    public async ValueTask<Result<Inspection>> Inspect(string packetDirectory, string repoDirectory, SizesFile? sizes, CancellationToken cancellationToken)
    {
        var loaded = await source.Load(packetDirectory, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var tree = await BaseTreeLoader.Load(repo, loaded.Value.Packet.Manifest.BaseSha, repoDirectory, cancellationToken).ConfigureAwait(false);
        return tree.IsError ? tree.Error : new Inspection(loaded.Value, PacketChecks.Run(loaded.Value.Packet, tree.Value, sizes));
    }
}
