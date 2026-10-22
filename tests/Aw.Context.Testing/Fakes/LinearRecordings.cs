using System.Text.Json.Nodes;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Testing.Fakes;

/// <summary>The recorded Linear connections, by root field, read from the fixture's 'linear/*.json' responses.</summary>
internal static class LinearRecordings
{
    /// <summary>Connection root field to its recording.</summary>
    public static readonly IReadOnlyDictionary<string, string> Connections = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["projects"] = "linear/projects.json",
        ["documents"] = "linear/documents.json",
        ["projectUpdates"] = "linear/project-updates.json",
        ["issues"] = "linear/issues.json",
    };

    /// <summary>Single-node root field to the connection it looks in.</summary>
    public static readonly IReadOnlyDictionary<string, string> Singles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["project"] = "projects",
        ["document"] = "documents",
        ["issue"] = "issues",
    };

    /// <summary>Every root field the fake answers, for error messages.</summary>
    public static string Supported => string.Join(", ", Connections.Keys.Concat(Singles.Keys).Order(StringComparer.Ordinal));

    /// <summary>The recorded nodes of a connection (a fresh copy each call).</summary>
    /// <param name="connection">The connection's root field.</param>
    /// <param name="options">The server's options (extra projects and comments), or null for the recordings alone.</param>
    public static List<JsonNode> Nodes(string connection, FakeServerOptions? options = null)
    {
        var recording = JsonNode.Parse(SplitPanesFixture.ReadText(Connections[connection]));
        List<JsonNode> nodes = [.. recording?["data"]?[connection]?["nodes"]?.AsArray().OfType<JsonNode>().Select(n => n.DeepClone()) ?? []];
        return options is null ? nodes : Extended(connection, nodes, options);
    }

    // The recordings plus what the server's options add: filler projects, and comments on one issue.
    private static List<JsonNode> Extended(string connection, List<JsonNode> nodes, FakeServerOptions options)
    {
        if (connection == "projects" && options.LinearExtraProjects > 0 && nodes.Count > 0)
        {
            nodes.AddRange(Enumerable.Range(1, options.LinearExtraProjects).Select(i => ExtraProject(nodes[0], i, options.LinearExtraProjectsTeam)));
        }
        else if (connection == "issues" && options.LinearExtraComments > 0)
        {
            AddComments(nodes, options.LinearExtraComments);
        }

        return nodes;
    }

    private static JsonNode ExtraProject(JsonNode template, int i, string team)
    {
        var project = template.DeepClone();
        var when = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i);
        project["id"] = "0e5a1b00-0000-4000-8000-" + (0x100000 + i).ToString("x12", System.Globalization.CultureInfo.InvariantCulture);
        project["name"] = "Filler project " + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        project["updatedAt"] = when.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        project["teams"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["id"] = "0e5a1b00-0000-4000-8000-000000000902", ["key"] = team, ["name"] = team }) };
        return project;
    }

    private static void AddComments(List<JsonNode> issues, int count)
    {
        var target = issues.Find(n => (string?)n["identifier"] == "SPL-105");
        if (target?["comments"]?["nodes"] is not JsonArray comments || comments.Count == 0)
        {
            return;
        }

        var template = comments[0]!;
        for (var i = 1; i <= count; i++)
        {
            var comment = template.DeepClone();
            comment["id"] = "0e5a1b00-0000-4000-8000-" + (0x200000 + i).ToString("x12", System.Globalization.CultureInfo.InvariantCulture);
            comment["body"] = "Extra comment " + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            comments.Add(comment);
        }
    }
}
