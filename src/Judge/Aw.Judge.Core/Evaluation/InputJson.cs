using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Evaluation;

/// <summary>How a question input is written for the digest and for storage: compact JSON, relaxed escaping.</summary>
public static class InputJson
{
    /// <summary>The compact JSON text of a node ('null' for JSON null).</summary>
    /// <param name="node">The node.</param>
    /// <returns>The text.</returns>
    public static string Write(JsonNode? node)
        => throw new NotSupportedException("red: write the input");

    /// <summary>The input digest: the first 16 lowercase hex characters of the SHA-256 of <see cref="Write"/> as UTF-8.</summary>
    /// <param name="node">The input, unredacted.</param>
    /// <returns>The digest.</returns>
    public static string Digest(JsonNode? node) =>
        throw new NotSupportedException("red: digest the input");
}
