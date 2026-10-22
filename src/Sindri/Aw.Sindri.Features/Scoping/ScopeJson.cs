using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>Reads the scoping facts as JSON trees: the facts are model and code output held as text, so the rules read them by name and a field that is absent is simply absent.</summary>
internal static class ScopeJson
{
    /// <summary>A string field, or null when the node is no object, the field is missing or it is not a string.</summary>
    internal static string? Str(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>An integer field, or null.</summary>
    internal static int? Int(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    /// <summary>A boolean field, or null.</summary>
    internal static bool? Bool(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    /// <summary>An array field, or null.</summary>
    internal static JsonArray? Arr(JsonNode? node, string name) => node is JsonObject obj ? obj[name] as JsonArray : null;

    /// <summary>An object field, or null.</summary>
    internal static JsonObject? Obj(JsonNode? node, string name) => node is JsonObject obj ? obj[name] as JsonObject : null;

    /// <summary>The non-null elements of an array node; none for anything else.</summary>
    internal static IEnumerable<JsonNode> Items(JsonNode? array) => array is JsonArray items ? items.OfType<JsonNode>() : [];

    /// <summary>The string elements of an array node; none for anything else.</summary>
    internal static IEnumerable<string> Strings(JsonNode? array) =>
        Items(array).Select(static n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>();

    /// <summary>The string elements of an array field.</summary>
    internal static IEnumerable<string> Strings(JsonNode? node, string name) => Strings(Arr(node, name));

    /// <summary>The elements of an array field.</summary>
    internal static IEnumerable<JsonNode> Items(JsonNode? node, string name) => Items(Arr(node, name));

    /// <summary>Adds a node to an array (the non-generic overload: the generic one is reflection-based and not trimmable).</summary>
    internal static void Append(JsonArray array, JsonNode node) => array.Add(node);

    /// <summary>A string array as a JSON array.</summary>
    internal static JsonArray ToArray(IEnumerable<string> values) => new([.. values.Select(static v => (JsonNode?)JsonValue.Create(v))]);

    /// <summary>The tree as UTF-8 JSON text, compact or indented (a writer, so no reflection-based serializer is needed).</summary>
    internal static string Write(JsonNode node, bool indented = false)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Whether two trees hold the same JSON value (object members in any order, array elements in order).</summary>
    internal static bool SameValue(JsonNode? left, JsonNode? right) => JsonNode.DeepEquals(left, right);
}
