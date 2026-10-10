using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Evaluation;

/// <summary>How a question input is written for the digest and for storage: compact JSON through the AwJson profile.</summary>
public static class InputJson
{
    /// <summary>The compact JSON text of a node ('null' for JSON null).</summary>
    /// <param name="node">The node.</param>
    /// <param name="options">The package's AwJson options (relaxed escaping, not indented).</param>
    /// <returns>The text.</returns>
    public static string Write(JsonNode? node, JsonSerializerOptions options) => node?.ToJsonString(options) ?? "null";

    /// <summary>The input digest: the first 16 lowercase hex characters of the SHA-256 of <see cref="Write"/> as UTF-8.</summary>
    /// <param name="node">The input, unredacted.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The digest.</returns>
    public static string Digest(JsonNode? node, JsonSerializerOptions options) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Write(node, options))))[..16];
}
