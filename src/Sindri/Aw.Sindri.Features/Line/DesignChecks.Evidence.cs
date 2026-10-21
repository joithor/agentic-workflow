using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

internal static partial class DesignChecks
{
    private static readonly string[] _blocking = ["CRITICAL", "HIGH"];
    private static readonly string[] _resolvable = ["MEDIUM", "LOW"];

    // DSN-ACC-003
    private static IEnumerable<LineFinding> Acceptance(DesignContext c)
    {
        foreach (var scopeItem in c.ScopeItems)
        {
            var id = LineJson.Str(scopeItem, "id") ?? "?";
            var own = c.Items.FirstOrDefault(i => string.Equals(LineJson.Str(i, "id"), id, StringComparison.Ordinal));
            if (own is null || !string.Equals(LineJson.Str(own, "text"), LineJson.Str(scopeItem, "text"), StringComparison.Ordinal))
            {
                yield return DesignContext.Fail("DSN-ACC-003", id, $"scope acceptance item {id} is missing from the design or its text changed (LC-ACC-001)", "Keep scope's ids and texts unchanged; supersede an item with a new one instead.");
            }
        }

        var scopeIds = c.ScopeItems.Select(static i => LineJson.Str(i, "id")).ToHashSet(StringComparer.Ordinal);
        foreach (var item in c.Items)
        {
            var id = LineJson.Str(item, "id") ?? "?";
            if (!scopeIds.Contains(id) && !LineJson.Strings(item, "cites").Any(c.IsDesignId))
            {
                yield return DesignContext.Fail("DSN-ACC-003", id, $"added acceptance item {id} cites no section or decision of the design", "Cite the section or decision that adds it.");
            }

            if (Superseded(c, item, id) is { } finding)
            {
                yield return finding;
            }
        }
    }

    private static LineFinding? Superseded(DesignContext c, JsonNode item, string id)
    {
        if (!string.Equals(LineJson.Str(item, "status"), "superseded", StringComparison.Ordinal))
        {
            return null;
        }

        var successor = LineJson.Str(item, "supersededBy");
        return successor is not null && c.Items.Any(i => string.Equals(LineJson.Str(i, "id"), successor, StringComparison.Ordinal))
            ? DesignContext.Warning("DSN-ACC-003", id, $"acceptance item {id} is superseded by {successor}: stop 2 shows it", "Say why in the design.")
            : DesignContext.Fail("DSN-ACC-003", id, $"superseded item {id} names no successor item", "Set supersededBy to the item that replaces it.");
    }

    // DSN-LENS-001, -002, -003
    private static IEnumerable<LineFinding> Lenses(DesignContext c)
    {
        if (c.Rounds.Count == 0)
        {
            yield return DesignContext.Fail("DSN-LENS-001", item: null, "no lens round is logged", "Run the lens rounds.");
            yield break;
        }

        foreach (var round in c.Rounds)
        {
            var name = $"round {Convert.ToString(LineJson.Int(round, "round"), CultureInfo.InvariantCulture)}";
            var lenses = LineJson.Items(round, "lenses").ToList();
            foreach (var missing in LineJson.Strings(round, "selected").Where(s => !lenses.Exists(l => string.Equals(LineJson.Str(l, "lens"), s, StringComparison.Ordinal) && string.Equals(LineJson.Str(l, "state"), "ran", StringComparison.Ordinal))))
            {
                yield return DesignContext.Fail("DSN-LENS-001", name, $"{name} did not run lens {missing}, which its pack selected", "Run every selected lens in each round.");
            }

            foreach (var same in lenses.Where(l => LineJson.Str(l, "model") is { } m && string.Equals(m, LineJson.Str(round, "writerModel"), StringComparison.Ordinal)))
            {
                yield return DesignContext.Warning("DSN-LENS-002", LineJson.Str(same, "lens"), $"lens {LineJson.Str(same, "lens")} ran on the writer's model", "Use a different model for the lens (W8).");
            }
        }

        foreach (var finding in OpenFindings(c, c.Rounds[^1]))
        {
            yield return finding;
        }

        foreach (var finding in Unresolved(c))
        {
            yield return finding;
        }
    }

    private static IEnumerable<LineFinding> OpenFindings(DesignContext c, JsonNode last) =>
        LineJson.Items(last, "lenses").SelectMany(static l => LineJson.Items(l, "findings"))
            .Where(f => Array.Exists(_blocking, b => string.Equals(b, LineJson.Str(f, "severity"), StringComparison.Ordinal)) && !c.Accepted.Contains(LineJson.Str(f, "id") ?? string.Empty))
            .Select(f => DesignContext.Fail("DSN-LENS-001", LineJson.Str(f, "id"), $"the last lens round leaves {LineJson.Str(f, "severity")} finding {LineJson.Str(f, "id")} that is not an accepted risk", "Fix it, or accept it at the lens desk."));

    private static IEnumerable<LineFinding> Unresolved(DesignContext c)
    {
        var resolved = LineJson.Items(c.Design, "resolutions").Where(static r => !string.IsNullOrWhiteSpace(LineJson.Str(r, "text"))).Select(static r => LineJson.Str(r, "finding")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return c.Rounds.SelectMany(static r => LineJson.Items(r, "lenses")).SelectMany(static l => LineJson.Items(l, "findings"))
            .Where(f => Array.Exists(_resolvable, v => string.Equals(v, LineJson.Str(f, "severity"), StringComparison.Ordinal)) && !resolved.Contains(LineJson.Str(f, "id") ?? string.Empty))
            .Select(f => DesignContext.Fail("DSN-LENS-003", LineJson.Str(f, "id"), $"{LineJson.Str(f, "severity")} finding {LineJson.Str(f, "id")} has no written resolution", "Answer it in design.resolutions."));
    }

    // DSN-ESC-001 (warn)
    private static IEnumerable<LineFinding> Escapes(DesignContext c)
    {
        var listed = LineJson.Items(c.Design, "escapes").Select(static e => LineJson.Str(e, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var scopeSurfaces = c.SurfaceIds.ToHashSet(StringComparer.Ordinal);
        var scopeItems = c.ScopeItems.Select(static i => LineJson.Str(i, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var outside = c.Sections.SelectMany(static s => LineJson.Strings(s, "covers")).Where(s => !scopeSurfaces.Contains(s))
            .Concat(c.Items.Select(static i => LineJson.Str(i, "id")).OfType<string>().Where(i => !scopeItems.Contains(i)));
        return outside.Where(id => !listed.Contains(id)).Distinct(StringComparer.Ordinal)
            .Select(id => DesignContext.Warning("DSN-ESC-001", id, $"{id} is not in the scope packet and not listed in escapes", "List it in escapes.json."));
    }
}
