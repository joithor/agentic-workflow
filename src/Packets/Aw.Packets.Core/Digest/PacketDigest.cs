using System.Buffers;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Digest;

/// <summary>
/// The packet's canonical hash (spec §4c Digests, version 1): SHA-256 over the canonical JSON of the object
/// '{"digestVersion":1,"files":{&lt;path&gt;: &lt;entry&gt;}}'. A JSON packet file's entry is the file itself (packet.json
/// without its 'status' and 'hash', which change when a packet is frozen); any other linked file's entry is
/// 'sha256:&lt;hex of its bytes&gt;'. Canonical JSON: object keys sorted by ordinal, no whitespace, the Kernel profile's
/// relaxed escaping, numbers as they are written in the file.
/// </summary>
public static class PacketDigest
{
    /// <summary>The canonical form's version, recorded with every hash.</summary>
    public const int Version = 1;

    private const string ManifestFile = "packet.json";
    private static readonly JsonWriterOptions _writer = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false };

    /// <summary>Computes the hash of a loaded packet.</summary>
    /// <param name="loaded">The packet and its files.</param>
    /// <returns>'sha256:&lt;64 hex digits&gt;', or AW-PACKETS-003 when a JSON file can't be put in canonical form (a duplicate key).</returns>
    public static Result<string> Compute(LoadedPacket loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var links = loaded.Packet.Manifest.Links;
        var json = new HashSet<string>(StringComparer.Ordinal) { ManifestFile, links.Decisions, links.OutOfScope, links.Tasks };
        var files = new JsonObject();
        foreach (var (path, bytes) in loaded.Files.OrderBy(static f => f.Key, StringComparer.Ordinal))
        {
            var entry = json.Contains(path) ? Parse(path, bytes) : Results.Success<JsonNode?>(Hex(bytes.Span));
            if (entry.IsError)
            {
                return entry.Error;
            }

            files[path] = entry.Value;
        }

        var document = new JsonObject { ["digestVersion"] = Version, ["files"] = files };
        return Hex(Canonical(document));
    }

    /// <summary>The canonical bytes of a JSON node: keys sorted by ordinal, no whitespace.</summary>
    /// <param name="node">The node.</param>
    /// <returns>UTF-8 bytes.</returns>
    public static byte[] Canonical(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, _writer))
        {
            Write(writer, node);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static Result<JsonNode?> Parse(string path, ReadOnlyMemory<byte> bytes)
    {
        try
        {
            var node = JsonNode.Parse(bytes.Span);
            if (path == ManifestFile && node is JsonObject manifest)
            {
                _ = manifest.Remove("status");
                _ = manifest.Remove("hash");
            }

            return Results.Success(node);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            return PacketsErrors.Malformed.With($"{path} has no canonical form: {ex.Message}");
        }
    }

    private static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var (key, value) in obj.OrderBy(static p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    Write(writer, value);
                }

                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                node.WriteTo(writer);
                break;
        }
    }

    private static string Hex(ReadOnlySpan<byte> bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
}
