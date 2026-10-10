using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Evaluation;

/// <summary>How a question input is written for the digest and for storage: compact JSON, relaxed escaping.</summary>
public static class InputJson
{
    private static readonly JsonWriterOptions _options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The compact JSON text of a node ('null' for JSON null).</summary>
    /// <param name="node">The node.</param>
    /// <returns>The text.</returns>
    public static string Write(JsonNode? node)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, _options))
        {
            if (node is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                node.WriteTo(writer);
            }
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>The input digest: the first 16 lowercase hex characters of the SHA-256 of <see cref="Write"/> as UTF-8.</summary>
    /// <param name="node">The input, unredacted.</param>
    /// <returns>The digest.</returns>
    public static string Digest(JsonNode? node) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Write(node))))[..16];
}
