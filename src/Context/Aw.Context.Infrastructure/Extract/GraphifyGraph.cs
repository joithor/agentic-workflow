using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Reads graphify's graph.json (an untrusted output of a third-party tool) into clusters: one per community, with the files its nodes come from.</summary>
internal static class GraphifyGraph
{
    /// <summary>The fewest files a community needs to be a candidate feature (provisional owner default 2026-10-14 night).</summary>
    public const int MinimumFiles = 2;

    private const double LowestConfidence = 0.1;
    private const double HighestConfidence = 0.9;

    /// <summary>Parses the graph.</summary>
    /// <param name="json">graph.json's text.</param>
    /// <param name="roots">The snapshot directory's names (as given and resolved): graphify writes absolute paths into it.</param>
    /// <returns>The clusters, in community order, or an error when the text isn't a graph.</returns>
    public static Result<IReadOnlyList<FileCluster>> Clusters(string json, IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return Clusters(bytes, roots);
    }

    /// <summary>Parses the graph from a stream (no whole-file string is made).</summary>
    /// <param name="json">graph.json's UTF-8 bytes.</param>
    /// <param name="roots">The snapshot directory's names (as given and resolved).</param>
    /// <returns>The clusters, in community order, or an error when the text isn't a graph.</returns>
    public static Result<IReadOnlyList<FileCluster>> Clusters(Stream json, IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(roots);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            {
                return Unusable("it has no nodes");
            }

            var community = new Dictionary<string, int>(StringComparer.Ordinal);
            var filesOf = new SortedDictionary<int, SortedSet<string>>();
            foreach (var node in nodes.EnumerateArray().Where(n => n.ValueKind == JsonValueKind.Object))
            {
                var id = Text(node, "id");
                if (id is null || !node.TryGetProperty("community", out var c) || !c.TryGetInt32(out var number))
                {
                    continue;
                }

                community[id] = number;
                if (Text(node, "source_file") is { } file && InSnapshot(file, roots) is { } relative)
                {
                    _ = filesOf.TryAdd(number, new SortedSet<string>(StringComparer.Ordinal));
                    _ = filesOf[number].Add(relative);
                }
            }

            var (inside, touching) = Cohesion(document.RootElement, community);
            return Results.Success<IReadOnlyList<FileCluster>>([.. filesOf.Where(f => f.Value.Count >= MinimumFiles).Select(f => new FileCluster(
                ClusterId.From(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"c{f.Key}")), [.. f.Value], Confidence(inside, touching, f.Key)))]);
        }
        catch (JsonException)
        {
            return Unusable("it isn't JSON");
        }
    }

    // The share of the edges touching a community that stay inside it: a tight group is a likelier feature.
    private static (Dictionary<int, int> Inside, Dictionary<int, int> Touching) Cohesion(JsonElement root, Dictionary<string, int> community)
    {
        Dictionary<int, int> inside = [];
        Dictionary<int, int> touching = [];
        var edges = root.TryGetProperty("links", out var links) ? links : root.TryGetProperty("edges", out var alt) ? alt : default;
        if (edges.ValueKind != JsonValueKind.Array)
        {
            return (inside, touching);
        }

        foreach (var edge in edges.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
        {
            if (Text(edge, "source") is not { } from || Text(edge, "target") is not { } to || !community.TryGetValue(from, out var a) || !community.TryGetValue(to, out var b))
            {
                continue;
            }

            touching[a] = touching.GetValueOrDefault(a) + 1;
            if (a == b)
            {
                inside[a] = inside.GetValueOrDefault(a) + 1;
            }
            else
            {
                touching[b] = touching.GetValueOrDefault(b) + 1;
            }
        }

        return (inside, touching);
    }

    private static double Confidence(Dictionary<int, int> inside, Dictionary<int, int> touching, int community) =>
        touching.GetValueOrDefault(community) == 0
            ? LowestConfidence
            : Math.Round(Math.Clamp((double)inside.GetValueOrDefault(community) / touching[community], LowestConfidence, HighestConfidence), 2, MidpointRounding.AwayFromZero);

    private static string? Text(JsonElement element, string name) =>
        !element.TryGetProperty(name, out var value) ? null
        : value.ValueKind == JsonValueKind.String ? value.GetString()
        : value.ValueKind == JsonValueKind.Number ? value.GetRawText()
        : null;

    // graphify writes absolute paths into the snapshot, which is deleted afterwards: keep them relative to it; a path outside it,
    // one that climbs out, one in graphify's own folders or one the code index couldn't key is dropped.
    private static string? InSnapshot(string file, IReadOnlyList<string> roots)
    {
        var relative = file;
        foreach (var root in roots.Where(r => file.StartsWith(r + "/", StringComparison.Ordinal)))
        {
            relative = file[(root.Length + 1)..];
            break;
        }

        if (ReferenceEquals(relative, file) && file.StartsWith('/'))
        {
            return null;
        }

        var segments = relative.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 || segments.Contains("..") || segments.Contains(".") || segments[0].StartsWith("graphify-out", StringComparison.Ordinal)
            || segments[0].StartsWith(GraphifyClusterSource.PrivateFolder, StringComparison.Ordinal)
            ? null
            : string.Join('/', segments);
    }

    private static Error Unusable(string why) => ContextErrors.SourceUnavailable.With($"graphify wrote an unusable graph.json: {why}");
}
