using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// graph.json, derived by code from the phases (line spec §6.2): each phase's wave (one more than its deepest dependency), its stack (it continues its first
/// dependency's stack unless an earlier phase already did, else it opens the next), its base rule (the default branch with no dependency, else the first dependency's
/// head) and whether it waits for merge (the plan's own flag, else more than one dependency). 'parallel' and 'maxStack' are the plan's, defaults 2 and 4.
/// </summary>
internal static class PlanGraph
{
    /// <summary>The graph file's object.</summary>
    /// <param name="plan">The 'fact:plan' tree.</param>
    /// <returns>The object, with schemaVersion.</returns>
    internal static JsonObject Derive(JsonNode? plan)
    {
        var phases = LineJson.Items(plan, "phases").ToList();
        var waves = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var phase in phases)
        {
            _ = Wave(phases, PlanContext.Id(phase), waves, []);
        }

        var stacks = new Dictionary<string, int>(StringComparer.Ordinal);
        var continued = new HashSet<string>(StringComparer.Ordinal);
        var entries = new JsonArray();
        foreach (var phase in phases.OrderBy(p => waves[PlanContext.Id(p)]).ThenBy(static p => PlanContext.Id(p), StringComparer.Ordinal))
        {
            LineJson.Append(entries, Entry(phase, waves, stacks, continued, LineJson.Str(plan, "defaultBranch") ?? "main"));
        }

        var graph = LinePackets.Doc(("parallel", LineJson.Int(plan, "parallel") ?? 2), ("maxStack", LineJson.Int(plan, "maxStack") ?? 4));
        graph["phases"] = Reorder(entries, phases);
        return graph;
    }

    private static JsonObject Entry(JsonNode phase, Dictionary<string, int> waves, Dictionary<string, int> stacks, HashSet<string> continued, string defaultBranch)
    {
        var id = PlanContext.Id(phase);
        var deps = LineJson.Strings(phase, "deps").ToList();
        var first = deps.Count > 0 ? deps[0] : null;
        var stack = first is not null && stacks.TryGetValue(first, out var inherited) && continued.Add(first) ? inherited : stacks.Values.DefaultIfEmpty(0).Max() + 1;

        stacks[id] = stack;
        return new JsonObject
        {
            ["id"] = id,
            ["stack"] = stack,
            ["wave"] = waves[id],
            ["base"] = new JsonObject { ["rule"] = first is null ? "default-branch" : "dependency-head", ["ref"] = first ?? defaultBranch },
            ["waitsForMerge"] = LineJson.Bool(phase, "waitsForMerge") ?? (deps.Count > 1),
        };
    }

    // The wave of a phase; a cycle (a plan the checks refuse) counts each phase once, so this always ends.
    private static int Wave(List<JsonNode> phases, string id, Dictionary<string, int> waves, HashSet<string> path)
    {
        if (waves.TryGetValue(id, out var known))
        {
            return known;
        }

        if (!path.Add(id) || phases.Find(p => string.Equals(PlanContext.Id(p), id, StringComparison.Ordinal)) is not { } phase)
        {
            return 0;
        }

        var deepest = LineJson.Strings(phase, "deps").Select(d => Wave(phases, d, waves, path)).DefaultIfEmpty(0).Max();
        _ = path.Remove(id);
        return waves[id] = deepest + 1;
    }

    // The entries in the plan's phase order, as the plan lists them.
    private static JsonArray Reorder(JsonArray entries, List<JsonNode> phases)
    {
        var ordered = new JsonArray();
        foreach (var phase in phases)
        {
            var entry = entries.OfType<JsonObject>().First(e => string.Equals(LineJson.Str(e, "id"), PlanContext.Id(phase), StringComparison.Ordinal));
            LineJson.Append(ordered, entry.DeepClone());
        }

        return ordered;
    }
}
