using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Trust;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Fences the untrusted free text of a sindri_* result (API spec §8, "the same trust field and fencing as context_* results"):
/// each planned string is wrapped by <see cref="UntrustedFence"/> and its object gains a trust member set to "untrusted". Nothing
/// else changes, so structuredContent equals the HTTP body apart from those values and members.
/// </summary>
internal static class McpFencing
{
    // why: the host's AwJson writes with this encoder, so unplanned strings come out as they went in
    private static readonly JsonSerializerOptions _write = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The member that marks an object as holding fenced text.</summary>
    internal const string TrustMember = "trust";

    /// <summary>The value of <see cref="TrustMember"/>.</summary>
    internal const string Untrusted = "untrusted";

    /// <summary>The body with every field of the plan fenced.</summary>
    /// <param name="body">The route's JSON body.</param>
    /// <param name="plan">What to fence.</param>
    /// <returns>The fenced body.</returns>
    internal static JsonElement Apply(JsonElement body, FencePlan plan)
    {
        var node = JsonNode.Parse(body.GetRawText());
        foreach (var field in plan.Fields)
        {
            Fence(node, field.Path.Split('.'), 0, plan.Source, field.AuthorField);
        }

        using var document = JsonDocument.Parse(node is null ? "null" : node.ToJsonString(_write));
        return document.RootElement.Clone();
    }

    private static void Fence(JsonNode? node, string[] path, int depth, string source, string? authorField)
    {
        var segment = path[depth];
        var many = segment.EndsWith("[]", StringComparison.Ordinal);
        var name = many ? segment[..^2] : segment;
        if (node is not JsonObject owner || owner[name] is not { } child)
        {
            return;
        }

        var last = depth == path.Length - 1;
        if (many)
        {
            foreach (var element in child is JsonArray array ? array.ToList() : [])
            {
                Fence(element, path, depth + 1, source, authorField);
            }
        }
        else if (!last)
        {
            Fence(child, path, depth + 1, source, authorField);
        }
        else
        {
            Wrap(owner, name, child, source, authorField);
        }
    }

    private static void Wrap(JsonObject owner, string name, JsonNode child, string source, string? authorField)
    {
        if (child is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return;
        }

        var author = authorField is not null && owner[authorField] is JsonValue who && who.TryGetValue<string>(out var by) ? by : null;
        owner[name] = UntrustedFence.Wrap(source, author, text);
        owner[TrustMember] = Untrusted;
    }
}
