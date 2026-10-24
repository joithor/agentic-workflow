using System.Collections.Frozen;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Pages a tool answer: every list keeps one window of items, and page.more says another page follows.
/// Counts and other objects stay the full answer. The section and group buckets stay whole, because they are the
/// answer's shape and their lists are what pages.
/// </summary>
internal static class McpPages
{
    // why: the host's AwJson writes with this encoder, so a page is the same text with lists cut
    private static readonly JsonSerializerOptions _write = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // why: these arrays are the buckets (one section or queue group each), not a list a caller pages through
    private static readonly FrozenSet<string> _buckets = FrozenSet.Create(StringComparer.Ordinal, "sections", "groups");

    /// <summary>The answer with one page of each list, and a page object.</summary>
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
        Slice(root, window, ref more);
        root["page"] = new JsonObject
        {
            ["offset"] = window.Offset,
            ["limit"] = window.Limit,
            ["more"] = more,
        };
        using var document = JsonDocument.Parse(root.ToJsonString(_write));
        return document.RootElement.Clone();
    }

    private static void Slice(JsonObject obj, McpWindow window, ref bool more)
    {
        foreach (var name in obj.Select(property => property.Key).ToArray())
        {
            switch (obj[name])
            {
                case JsonArray array when _buckets.Contains(name):
                    foreach (var item in array)
                    {
                        if (item is JsonObject child)
                        {
                            Slice(child, window, ref more);
                        }
                    }

                    break;
                case JsonArray array:
                    obj[name] = Cut(array, window, ref more);
                    break;
                case JsonObject child:
                    Slice(child, window, ref more);
                    break;
                default:
                    break;
            }
        }
    }

    private static JsonArray Cut(JsonArray array, McpWindow window, ref bool more)
    {
        if (array.Count > window.Offset + window.Limit)
        {
            more = true;
        }

        var cut = new JsonArray();
        if (window.Offset >= array.Count)
        {
            return cut;
        }

        var count = Math.Min(window.Limit, array.Count - window.Offset);
        for (var index = 0; index < count; index++)
        {
            var item = array[window.Offset + index]?.DeepClone();
            if (item is JsonObject child)
            {
                Slice(child, window, ref more);
            }

            cut.Add(item);
        }

        return cut;
    }
}
