using System.Text.Json;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>Reads a line packet's repository and id from its packet.json, with no repository access (verify locates the freeze record by them).</summary>
public static class LinePacketIdentity
{
    /// <summary>Reads the identity.</summary>
    /// <param name="kind">A line kind.</param>
    /// <param name="files">The packet as read.</param>
    /// <param name="json">The package's JSON options.</param>
    /// <returns>The identity; null when packet.json is missing or doesn't parse.</returns>
    public static PacketIdentity? Of(PacketKind kind, LinePacketFiles files, JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(files);
        return LinePacketChecks.ReadManifest(kind, files, json).Manifest is { } manifest ? new PacketIdentity(manifest.Repo, manifest.Id) : null;
    }
}
