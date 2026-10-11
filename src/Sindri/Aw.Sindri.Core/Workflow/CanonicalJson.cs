using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>RFC 8785 (JSON Canonicalization Scheme): what the recipe hash (spec §3.1) and the descriptor hash (spec §2) are computed over. Red stub (P1 unit (a)).</summary>
public static class CanonicalJson
{
    /// <summary>The canonical UTF-8 bytes of <paramref name="node"/>.</summary>
    /// <param name="node">A JSON value; null is JSON null.</param>
    /// <returns>The canonical form.</returns>
    public static byte[] Of(JsonNode? node) => throw new NotSupportedException("red: RFC 8785 lands with P1 unit (a)");
}
