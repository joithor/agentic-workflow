using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Line;

/// <summary>Reads a packet's files as JSON trees: the files are model and code output held as text, so the rules read them by name and a field that is absent is simply absent.</summary>
public static class LineJson
{
    /// <summary>A string field, or null when the node is no object, the field is missing or it is not a string.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The text or null.</returns>
    public static string? Str(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>An integer field, or null.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The number or null.</returns>
    public static int? Number(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    /// <summary>A boolean field, or null.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The flag or null.</returns>
    public static bool? Bool(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    /// <summary>An array field, or null.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The array or null.</returns>
    public static JsonArray? Arr(JsonNode? node, string name) => node is JsonObject obj ? obj[name] as JsonArray : null;

    /// <summary>An object field, or null.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The object or null.</returns>
    public static JsonObject? Obj(JsonNode? node, string name) => node is JsonObject obj ? obj[name] as JsonObject : null;

    /// <summary>The non-null elements of an array field; none for anything else.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The elements.</returns>
    public static IEnumerable<JsonNode> Items(JsonNode? node, string name) => Items(Arr(node, name));

    /// <summary>The non-null elements of an array node; none for anything else.</summary>
    /// <param name="array">The array node.</param>
    /// <returns>The elements.</returns>
    public static IEnumerable<JsonNode> Items(JsonNode? array) => array is JsonArray items ? items.OfType<JsonNode>() : [];

    /// <summary>The string elements of an array field.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The strings.</returns>
    public static IEnumerable<string> Strings(JsonNode? node, string name) => Strings(Arr(node, name));

    /// <summary>The string elements of an array node; none for anything else.</summary>
    /// <param name="array">The array node.</param>
    /// <returns>The strings.</returns>
    public static IEnumerable<string> Strings(JsonNode? array) =>
        Items(array).Select(static n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>();
}
