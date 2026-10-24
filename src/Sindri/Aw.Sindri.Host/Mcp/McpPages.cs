using System.Collections.Frozen;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Pages a tool answer: every top-level list keeps its own window of items (the same offset and limit, applied to each list
/// by its own index, so a page is stable whatever the other lists hold), and page.more says another page follows in some
/// list. An item is never cut inside: its evidence, sources and other arrays stay whole. Counts and other objects stay the
/// full answer. The section and group buckets stay whole, because they are the answer's shape and their lists are what pages.
/// </summary>
internal static class McpPages
{
    // why: the host's AwJson writes with this encoder, so a page is the same text with lists cut
    private static readonly JsonSerializerOptions _write = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // why: these arrays are the buckets (one section or queue group each), not a list a caller pages through
    private static readonly FrozenSet<string> _buckets = FrozenSet.Create(StringComparer.Ordinal, "sections", "groups");

    /// <summary>The answer with one page of each top-level list, and a page object.</summary>
    /// <param name="answer">The whole answer.</param>
    /// <param name="window">Which page.</param>
    /// <returns>The page. A non-object answer is returned unchanged.</returns>
    internal static JsonElement Apply(JsonElement answer, McpWindow window)
    {
        if (JsonNode.Parse(answer.GetRawText()) is not JsonObject root)
        {
            return answer;
        }

        var more = false;
        var lists = new JsonObject();
        Slice(root, window, "", lists, ref more);
        root["page"] = new JsonObject
        {
            ["offset"] = window.Offset,
            ["limit"] = window.Limit,
            ["more"] = more,
            ["lists"] = lists,
        };
        using var document = JsonDocument.Parse(root.ToJsonString(_write));
        return document.RootElement.Clone();
    }

    // Only the object's own arrays are lists; a bucket's objects are sliced one level down, and nothing is sliced inside an item.
    private static void Slice(JsonObject obj, McpWindow window, string path, JsonObject lists, ref bool more)
    {
        foreach (var name in obj.Select(property => property.Key).ToArray())
        {
            if (obj[name] is not JsonArray array)
            {
                continue;
            }

            if (path.Length == 0 && _buckets.Contains(name))
            {
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonObject child)
                    {
                        Slice(child, window, $"{name}[{index}].", lists, ref more);
                    }
                }

                continue;
            }

            obj[name] = Cut(array, window, path + name, lists, ref more);
        }
    }

    private static JsonArray Cut(JsonArray array, McpWindow window, string path, JsonObject lists, ref bool more)
    {
        var listMore = (long)array.Count > (long)window.Offset + window.Limit;
        more |= listMore;
        lists[path] = new JsonObject { ["total"] = array.Count, ["more"] = listMore };
        var cut = new JsonArray();
        if (window.Offset >= array.Count)
        {
            return cut;
        }

        var count = Math.Min(window.Limit, array.Count - window.Offset);
        for (var index = 0; index < count; index++)
        {
            cut.Add(array[window.Offset + index]?.DeepClone());
        }

        return cut;
    }
}
