using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The phase packets (line spec §3.2 row 4), made by code from the plan and the design: phase.json (goal from the phase's goal or title, kind, deps, size, base from graph.json,
/// allowedFiles, testChange, readSeeds, budget), acceptance.json (the design's items of this phase, its parent the design's acceptance.json digest), signals.json (the
/// signals of those items), holdout.json (the set's hash and count), design.md (the spec's sections that cite those items) and index.json (parent: the plan packet).
/// </summary>
internal static class PhasePackets
{
    /// <summary>One phase's packet directory name below the run area's phase directory.</summary>
    /// <param name="phase">The phase id.</param>
    /// <returns>The directory name.</returns>
    internal static string Directory(string phase) => LinePackets.PhaseDirectory + "/" + LinePackets.Slug(phase);

    /// <summary>The packet id of a phase: '&lt;slug&gt;-&lt;phase&gt;' (PKT-PHS-007).</summary>
    /// <param name="scope">The 'fact:scope' object.</param>
    /// <param name="phase">The phase id.</param>
    /// <returns>The id.</returns>
    internal static string Id(JsonNode scope, string phase) => PlanPacket.Slug(scope) + "-" + LinePackets.Slug(phase);

    /// <summary>The files of one phase's packet; packet.json last.</summary>
    /// <param name="scope">The 'fact:scope' object.</param>
    /// <param name="design">The 'fact:design' object as the plan intake loaded it.</param>
    /// <param name="phase">The phase, from 'fact:plan'.</param>
    /// <param name="context">The plan packet's hash, the graph's entry for the phase, the holdout set, the repo slug.</param>
    /// <returns>The files; why the phase cannot be a packet.</returns>
    internal static Result<IReadOnlyList<PacketText>> Assemble(JsonNode scope, JsonNode design, JsonNode phase, (string PlanHash, JsonNode? GraphEntry, JsonNode? Set, string Repo) context)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(phase);
        var id = PlanContext.Id(phase);
        var holdout = context.Set is null ? null : LineJson.Str(context.Set, "hash");
        if (string.IsNullOrWhiteSpace(holdout) || LineJson.Str(scope, "baseSha") is not { Length: > 0 } baseSha)
        {
            return SindriErrors.InvalidInput.With($"phase {id} has no holdout set hash, or the scope has no baseSha (fix: run the holdout Step, then the plan station's evidence)");
        }

        var items = PlanContext.Items(phase).Select(static i => LineJson.Str(i, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var acceptance = LinePackets.Json(LinePackets.Doc(("parent", design["acceptanceHash"]), ("items", Filter(LineJson.Obj(design, "acceptance"), "items", "id", items))));
        return new List<PacketText>
        {
            new("phase.json", LinePackets.Json(Phase(phase, context.GraphEntry))),
            new("acceptance.json", acceptance),
            new("signals.json", LinePackets.Json(LinePackets.Doc(("signals", Filter(design, "signals", "item", items))))),
            new("holdout.json", LinePackets.Json(LinePackets.Doc(("setHash", holdout), ("count", LineJson.Int(context.Set, "count") ?? 0)))),
            new("design.md", Sections(design, items)),
            new("index.json", LinePackets.Json(new JsonObject
            {
                ["schemaVersion"] = 1,
                ["kind"] = "phase",
                ["parents"] = new JsonArray(context.PlanHash),
                ["acceptance"] = new JsonObject { ["self"] = LinePackets.Sha256(acceptance), ["extends"] = design["acceptanceHash"]?.DeepClone() },
            })),
            new("packet.json", LinePackets.Json(new JsonObject
            {
                ["schemaVersion"] = 1,
                ["kind"] = "phase",
                ["id"] = Id(scope, id),
                ["repo"] = context.Repo,
                ["baseSha"] = baseSha,
                ["status"] = "draft",
                ["hash"] = null,
                ["links"] = new JsonObject { ["phase"] = "phase.json", ["acceptance"] = "acceptance.json", ["signals"] = "signals.json", ["holdout"] = "holdout.json", ["design"] = "design.md", ["index"] = "index.json" },
            })),
        };
    }

    private static JsonObject Phase(JsonNode phase, JsonNode? graphEntry)
    {
        var testChange = new JsonArray();
        foreach (var change in PlanPacket.TestChanges(phase))
        {
            LineJson.Append(testChange, change);
        }

        return LinePackets.Doc(
            ("goal", LineJson.Str(phase, "goal") ?? LineJson.Str(phase, "title") ?? PlanContext.Id(phase)),
            ("kind", LineJson.Str(phase, "kind") ?? "feature"),
            ("deps", LineJson.Arr(phase, "deps") ?? []),
            ("size", LineJson.Str(phase, "size")),
            ("base", LineJson.Obj(graphEntry, "base")),
            ("allowedFiles", LineJson.Arr(phase, "writeScope") ?? LineJson.Arr(phase, "allowedFiles") ?? []),
            ("testChange", testChange),
            ("readSeeds", LineJson.Arr(phase, "readSeeds") ?? []),
            ("budget", LineJson.Obj(phase, "budget") ?? []));
    }

    private static JsonArray Filter(JsonNode? owner, string list, string key, HashSet<string> ids)
    {
        var kept = new JsonArray();
        foreach (var node in LineJson.Items(owner, list).Where(n => LineJson.Str(n, key) is { } id && ids.Contains(id)))
        {
            LineJson.Append(kept, node.DeepClone());
        }

        return kept;
    }

    // The spec's sections that cite one of the phase's items, each cut out of spec.md from its heading to the next heading of the same or a higher level.
    private static string Sections(JsonNode design, HashSet<string> items)
    {
        var lines = (LineJson.Str(design, "spec") ?? string.Empty).Split('\n');
        var text = new StringBuilder("# Design sections for this phase\n");
        var any = false;
        foreach (var section in LineJson.Items(design, "sections").Where(s => LineJson.Strings(s, "cites").Any(items.Contains)))
        {
            any = true;
            _ = text.Append('\n').Append(Cut(lines, LineJson.Str(section, "heading") ?? LineJson.Str(section, "id") ?? "section"));
        }

        return any ? text.ToString() : text.Append("\nNo section of the design cites this phase's items.\n").ToString();
    }

    private static int Level(string line) => line.Length - line.TrimStart('#').Length;

    private static string Cut(string[] lines, string heading)
    {
        var start = Array.FindIndex(lines, l => l.StartsWith('#') && l.Contains(heading, StringComparison.Ordinal));
        if (start < 0)
        {
            return "## " + heading + "\n";
        }

        var level = Level(lines[start]);
        var end = Array.FindIndex(lines, start + 1, l => l.StartsWith('#') && Level(l) <= level);
        return string.Join('\n', lines[start..(end < 0 ? lines.Length : end)]).TrimEnd() + "\n";
    }
}
