using System.Text.Json;
using Aw.Kernel;
using Aw.Packets.Core.Checks;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>Reads a line packet and runs its schema checks (LN-PKT1) and PKT-BASE-001 against its base commit.</summary>
/// <param name="source">Reads the packet directory.</param>
/// <param name="repo">Lists the repository's files at the base commit.</param>
/// <param name="json">The package's JSON options.</param>
public sealed class LinePacketInspector(ILinePacketSource source, IRepoSnapshot repo, JsonSerializerOptions json)
{
    /// <summary>Reads and checks the packet in <paramref name="packetDirectory"/>.</summary>
    /// <param name="kind">A line kind.</param>
    /// <param name="packetDirectory">The packet directory.</param>
    /// <param name="repoDirectory">A directory inside the repository.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The inspection; the read error; or AW-PACKETS-006 when the repository can't be read. A base commit
    /// missing from the repository is a PKT-BASE-001 failure, not an error.</returns>
    public async ValueTask<Result<LineInspection>> Inspect(PacketKind kind, string packetDirectory, string repoDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        var files = await source.Read(kind, packetDirectory, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error;
        }

        var read = LinePacketChecks.ReadManifest(kind, files.Value, json);
        var manifest = read.Manifest;
        BaseTree? tree = null;
        if (manifest is not null)
        {
            var loaded = await BaseTreeLoader.Load(repo, manifest.BaseSha, repoDirectory, cancellationToken).ConfigureAwait(false);
            if (loaded.IsError)
            {
                return loaded.Error;
            }

            tree = loaded.Value;
        }

        return new LineInspection(kind, files.Value, manifest, LinePacketChecks.Run(kind, files.Value, read, tree, json));
    }
}
