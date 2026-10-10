using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Packets.Core.Checks;

namespace Aw.Packets.Core;

/// <summary>Loads a packet and runs the deterministic checks (addendum §17.4) against its base commit.</summary>
/// <param name="source">Reads the packet directory.</param>
/// <param name="repo">Lists the repository's files at the base commit.</param>
public sealed partial class PacketInspector(IPacketSource source, IRepoSnapshot repo)
{
    /// <summary>Loads and checks the packet in <paramref name="packetDirectory"/>.</summary>
    /// <param name="packetDirectory">The packet directory.</param>
    /// <param name="repoDirectory">A directory inside the repository.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The inspection; the load error; or AW-PACKETS-006 when the repository can't be read. A base commit
    /// missing from the repository is a PKT-BASE-001 failure, not an error.</returns>
    public async ValueTask<Result<Inspection>> Inspect(string packetDirectory, string repoDirectory, CancellationToken cancellationToken)
    {
        var loaded = await source.Load(packetDirectory, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var tree = await Base(loaded.Value.Packet.Manifest.BaseSha, repoDirectory, cancellationToken).ConfigureAwait(false);
        return tree.IsError ? tree.Error : new Inspection(loaded.Value, PacketChecks.Run(loaded.Value.Packet, tree.Value));
    }

    private async ValueTask<Result<BaseTree>> Base(string sha, string repoDirectory, CancellationToken cancellationToken)
    {
        if (!FullSha.IsMatch(sha))
        {
            return new BaseTree(Files: null, $"baseSha '{sha}' isn't a full commit sha (40 or 64 lower-case hex digits)");
        }

        var files = await repo.Files(repoDirectory, sha, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error.Code == PacketsErrors.CommitNotFound.Code
                ? new BaseTree(Files: null, $"baseSha {sha} isn't in the repository: {files.Error.Message}")
                : files.Error;
        }

        return new BaseTree(files.Value, Problem: null);
    }

    [GeneratedRegex("^(?:[0-9a-f]{40}|[0-9a-f]{64})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FullSha { get; }
}
