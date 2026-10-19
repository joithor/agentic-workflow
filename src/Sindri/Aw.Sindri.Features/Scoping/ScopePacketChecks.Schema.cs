using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    private static readonly (string Fact, string Schema)[] _modelFacts = [("scopeMap", "scope-map"), ("acceptance", "acceptance")];

    /// <summary>SCP-SCHEMA-001: the draft's facts against their schemas' fact form directly (a model can add 'schemaVersion' to dodge a switch on it), and every fact must be JSON.</summary>
    private static void Schema(ScopeContext c, RuleSink s)
    {
        foreach (var (key, why) in c.Facts.Unparseable)
        {
            s.Fail(key, $"fact:{key} is not JSON: {why}", $"Write fact:{key} as one JSON value.");
        }

        if (!_modelFacts.Any(m => c.Facts.Has(m.Fact)) && c.Facts.Unparseable.Count == 0)
        {
            _ = s.Need("scopeMap", "acceptance");
            return;
        }

        foreach (var (key, schema) in _modelFacts.Where(m => c.Facts.Has(m.Fact)))
        {
            foreach (var error in ScopeSchemas.Errors(schema, c.Facts[key]).Where(static e => !e.Path.EndsWith("/trust", StringComparison.Ordinal)))
            {
                s.Fail(key, $"fact:{key}{error.Path}: {error.Message}", $"Make fact:{key} match schemas/scoping/{schema}.json (its fact form).");
            }
        }
    }

    /// <summary>SCP-TRUST-001 at the draft stage: models never write 'trust'; code stamps it at the evidence step.</summary>
    private static void NoModelTrust(ScopeContext c, RuleSink s)
    {
        if (!_modelFacts.Any(m => c.Facts.Has(m.Fact)))
        {
            _ = s.Need("scopeMap", "acceptance");
            return;
        }

        foreach (var node in Cited(c).Where(static n => n.Node is JsonObject o && o.ContainsKey("trust")))
        {
            s.Fail(node.Id, $"{node.Id} carries 'trust', which only the evidence step may stamp", "Remove 'trust' from the item; the evidence step stamps it.");
        }
    }

    /// <summary>A map or acceptance entry that cites something: its id, its tree and its citations.</summary>
    private sealed record CitedItem(string Id, JsonNode Node, IReadOnlyList<string> Citations);

    /// <summary>Every surface, implication and acceptance item that is present, with its citations.</summary>
    private static IEnumerable<CitedItem> Cited(ScopeContext c)
    {
        foreach (var surface in ScopeJson.Items(c.Facts["scopeMap"], "surfaces"))
        {
            yield return new CitedItem(ScopeJson.Str(surface, "id") ?? "?", surface, [.. ScopeJson.Strings(surface, "citations")]);
        }

        foreach (var implication in ScopeJson.Items(c.Facts["scopeMap"], "implications"))
        {
            yield return new CitedItem(ScopeJson.Str(implication, "id") ?? "?", implication, [.. ScopeJson.Strings(implication, "citations")]);
        }

        foreach (var item in ScopeJson.Items(c.Facts["acceptance"], "items"))
        {
            yield return new CitedItem(ScopeJson.Str(item, "id") ?? "?", item, [.. ScopeJson.Strings(item, "citations")]);
        }
    }

    /// <summary>SCP-CITE-001 (an item that cites nothing) and SCP-CITE-002 (an item none of whose citations resolves).</summary>
    private static void Cite(ScopeContext c, RuleSink s, bool uncited)
    {
        if (!_modelFacts.Any(m => c.Facts.Has(m.Fact)) || !s.Need("lenses", "brief", "answerLog"))
        {
            if (s.Missing.Count == 0)
            {
                _ = s.Need("scopeMap", "acceptance");
            }

            return;
        }

        foreach (var item in Cited(c))
        {
            if (item.Citations.Count == 0)
            {
                if (uncited)
                {
                    s.Fail(item.Id, $"{item.Id} cites nothing", "Cite a lens item (L-<lens>-<n>), a source ref (R<n>) or an answer (A<n>) that resolves.");
                }
            }
            else if (!uncited && !item.Citations.Any(c.Ids.Resolves))
            {
                s.Fail(item.Id, $"{item.Id} cites {string.Join(", ", item.Citations)}, and none resolves", "Cite an id that exists in the lenses, the brief's refs or the answer log.");
            }
        }
    }
}
