using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Packets.App;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Testing;

/// <summary>Writes a packet as the files a packet directory holds (indented AwJson, design docs as short text).</summary>
internal static class PacketFiles
{
    /// <summary>The JSON options packet files are read and written with.</summary>
    public static JsonSerializerOptions Json { get; } = AwJson.CreateOptions(PacketsJsonContext.Default, indented: true);

    /// <summary>The files for <paramref name="packet"/>, by path relative to the packet directory.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>A mutable map, so a test can break one file.</returns>
    public static Dictionary<string, byte[]> Of(Packet packet)
    {
        var links = packet.Manifest.Links;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["packet.json"] = AwJson.Serialize(packet.Manifest, Json),
            [links.Decisions] = AwJson.Serialize(packet.Decisions, Json),
            [links.OutOfScope] = AwJson.Serialize(packet.OutOfScope, Json),
            [links.Tasks] = AwJson.Serialize(packet.Tasks, Json),
        };
        foreach (var doc in links.Design.Concat(links.Plans))
        {
            files[doc] = Encoding.UTF8.GetBytes($"# {doc}\n");
        }

        return files;
    }
}
