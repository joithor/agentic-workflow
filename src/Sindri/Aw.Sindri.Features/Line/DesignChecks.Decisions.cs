using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

internal static partial class DesignChecks
{
    // DSN-DEC-001
    private static IEnumerable<LineFinding> Decisions(DesignContext c)
    {
        foreach (var decision in c.Decisions)
        {
            var id = LineJson.Str(decision, "id") ?? "?";
            var options = LineJson.Items(decision, "options").ToList();
            if (options.Count is < 2 or > 3)
            {
                yield return DesignContext.Fail("DSN-DEC-001", id, $"decision {id} has {options.Count} options; it needs two or three", "Offer two or three options.");
            }

            if (options.Where(static o => LineJson.Bool(o, "recommended") == true).Take(2).Count() != 1)
            {
                yield return DesignContext.Fail("DSN-DEC-001", id, $"decision {id} must have exactly one recommended option", "Mark exactly one option recommended.");
            }

            foreach (var option in options.Where(o => string.IsNullOrWhiteSpace(LineJson.Str(o, "tradeoffs")) || !HasCitations(decision, o)))
            {
                yield return DesignContext.Fail("DSN-DEC-001", LineJson.Str(option, "id") ?? id, $"an option of decision {id} lacks its trade-offs or citations", "Give every option trade-offs and cite scope ids.");
            }

            foreach (var finding in Affected(c, decision, id, options))
            {
                yield return finding;
            }
        }
    }

    private static bool HasCitations(JsonNode decision, JsonNode option) => LineJson.Strings(option, "cites").Any() || LineJson.Strings(decision, "cites").Any();

    private static IEnumerable<LineFinding> Affected(DesignContext c, JsonNode decision, string id, List<JsonNode> options)
    {
        var affects = LineJson.Strings(decision, "affects").ToList();
        if (affects.Count == 0)
        {
            yield return DesignContext.Fail("DSN-DEC-001", id, $"decision {id} affects no section", "List the sections that depend on it in affects.");
        }

        var recommended = options.Find(static o => LineJson.Bool(o, "recommended") == true) is { } r ? LineJson.Str(r, "id") : null;
        foreach (var name in affects.Where(n => !Marked(c, n, id, recommended)))
        {
            yield return DesignContext.Fail("DSN-DEC-001", name, $"section {name} is affected by decision {id} but does not carry the marker assumes: {recommended ?? id}", $"Write the section as if {recommended ?? "the recommended option"} were chosen and mark it assumes: {recommended ?? id}.");
        }
    }

    private static bool Marked(DesignContext c, string sectionId, string decisionId, string? recommended) =>
        c.Sections.FirstOrDefault(s => string.Equals(LineJson.Str(s, "id"), sectionId, StringComparison.Ordinal)) is { } section
        && LineJson.Strings(section, "assumes").Any(a => a.StartsWith(decisionId + ".", StringComparison.Ordinal) && (recommended is null || string.Equals(a, recommended, StringComparison.Ordinal)));
}
