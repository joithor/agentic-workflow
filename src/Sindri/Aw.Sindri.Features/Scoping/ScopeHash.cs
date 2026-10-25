using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The packet hash fact:scopePacket holds: { "hash": "sha256:…" }, or the hash as a string.</summary>
internal static class ScopeHash
{
    /// <summary>The hash, or null when the fact is absent or holds none.</summary>
    internal static string? Of(string? factText)
    {
        if (factText is null)
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(factText);
            return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : ScopeJson.Str(node, "hash");
        }
        catch (JsonException)
        {
            return null;   // boundary: a fact is text a person wrote
        }
    }
}
