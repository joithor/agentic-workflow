using System.Text;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The scoping graph fact (issue 1005): the prefiltered graph the agent Steps read in place of the lens dump. When the context engine answered the graph input its view is the fact
/// (its size recounted); when it did not, the fact is a projection of the lenses fact (no rank beyond the lenses' own order, no summaries beyond labels) with a recorded limit,
/// so every Step still finds a graph.json. Deterministic, no I/O.
/// </summary>
internal static class GraphGather
{
    private const int Version = 1;
    private const string NoGraph = "no-graph";
    private const string CodePrefix = "code:";

    /// <summary>The graph fact.</summary>
    /// <param name="graphView">The context engine's ScopeGraphView as JSON, or null when the graph input gave no result.</param>
    /// <param name="lenses">The lenses fact, as <see cref="LensGather.Assemble"/> made it.</param>
    /// <param name="anchors">The anchors that were asked.</param>
    internal static JsonObject Assemble(JsonNode? graphView, JsonObject lenses, IReadOnlyList<string> anchors)
    {
        ArgumentNullException.ThrowIfNull(lenses);
        var graph = graphView is JsonObject view ? FromView(view) : FromLenses(lenses, anchors);
        Size(graph);
        return graph;
    }

    private static JsonObject FromView(JsonObject view) => new()
    {
        ["version"] = Version,
        ["mapVersion"] = ScopeJson.Str(view, "mapVersion") ?? "unknown",
        ["anchors"] = ScopeJson.ToArray(ScopeJson.Strings(view, "anchors")),
        ["depth"] = ScopeJson.Int(view, "depth") ?? 0,
        ["hops"] = ScopeJson.ToArray(ScopeJson.Strings(view, "hops")),
        ["nodes"] = new JsonArray([.. ScopeJson.Items(view, "nodes").Select(static n => n.DeepClone())]),
        ["edges"] = new JsonArray([.. ScopeJson.Items(view, "edges").Select(static e => e.DeepClone())]),
        ["limits"] = new JsonArray([.. ScopeJson.Items(view, "limits").Select(static l => l.DeepClone())]),
    };

    private static JsonObject FromLenses(JsonObject lenses, IReadOnlyList<string> anchors)
    {
        var nodes = new JsonArray();
        var edges = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        var rank = 0;
        foreach (var lens in ScopeRules.Lenses)
        {
            var entry = ScopeJson.Obj(lenses, lens);
            foreach (var node in ScopeJson.Items(entry, "nodes"))
            {
                Add(nodes, seen, ScopeJson.Str(node, "key"), node, lens, ref rank);
            }

            foreach (var item in ScopeJson.Items(entry, "items"))
            {
                Add(nodes, seen, ScopeJson.Strings(item, "citations").FirstOrDefault() ?? ScopeJson.Str(item, "lensItem"), item, lens, ref rank);
            }

            foreach (var edge in ScopeJson.Items(entry, "edges"))
            {
                var from = ScopeJson.Str(edge, "from") ?? string.Empty;
                var to = ScopeJson.Str(edge, "to") ?? string.Empty;
                var type = ScopeJson.Str(edge, "type") ?? "relates";
                if (seenEdges.Add($"{from}\n{to}\n{type}"))
                {
                    edges.Add(new JsonObject { ["from"] = from, ["to"] = to, ["type"] = type });
                }
            }
        }

        var related = ScopeJson.Obj(lenses, "related");
        return new JsonObject
        {
            ["version"] = Version,
            ["mapVersion"] = ScopeJson.Str(related, "mapVersion") ?? "unknown",
            ["anchors"] = ScopeJson.ToArray(anchors),
            ["depth"] = ScopeJson.Int(related, "depth") ?? 0,
            ["hops"] = ScopeJson.ToArray(ScopeJson.Strings(related, "hops")),
            ["nodes"] = nodes,
            ["edges"] = edges,
            ["limits"] = new JsonArray(new JsonObject { ["why"] = NoGraph, ["not"] = "ranking, summaries and role filtering: the context engine gave no graph, so this is the lenses' own order" }),
        };
    }

    private static void Add(JsonArray nodes, HashSet<string> seen, string? id, JsonNode source, string lens, ref int rank)
    {
        if (string.IsNullOrEmpty(id) || !seen.Add(id))
        {
            return;
        }

        var label = ScopeJson.Str(source, "label") ?? id;
        var node = new JsonObject
        {
            ["id"] = id,
            ["kind"] = ScopeJson.Str(source, "kind") ?? "node",
            ["summary"] = label,
            ["rank"] = ++rank,
            ["lens"] = lens,
            ["lensItem"] = ScopeJson.Str(source, "lensItem") ?? string.Empty,
            ["citations"] = new JsonArray([.. ScopeJson.Strings(source, "citations").Select(static key => (JsonNode?)new JsonObject { ["key"] = key })]),
        };
        if (id.StartsWith(CodePrefix, StringComparison.Ordinal))
        {
            node["path"] = id[CodePrefix.Length..];   // why: a path is on code: nodes only, derived from the key
        }

        if (ScopeJson.Str(source, "band") is { } band)
        {
            node["band"] = band;
        }

        nodes.Add(node);
    }

    // The size is recounted here, never trusted from the view: the brief tells the agent to read it first.
    private static void Size(JsonObject graph)
    {
        var size = new JsonObject { ["nodes"] = ScopeJson.Items(graph, "nodes").Count(), ["edges"] = ScopeJson.Items(graph, "edges").Count(), ["bytes"] = 0 };
        graph["size"] = size;
        size["bytes"] = Encoding.UTF8.GetByteCount(graph.ToJsonString());
    }
}
