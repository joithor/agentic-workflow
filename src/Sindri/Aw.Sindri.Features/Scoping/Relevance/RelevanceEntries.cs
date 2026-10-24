using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>One thing a lens returned: a node (it has a graph or file key) or an item (a record, decision or commit; no key).</summary>
/// <param name="Id">The lens item id, 'L-&lt;lens&gt;-&lt;n&gt;'.</param>
/// <param name="Lens">The lens it was found by.</param>
/// <param name="Key">The node key; null for an item.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Label">Its label.</param>
/// <param name="Band">The node's band; null for an item.</param>
internal sealed record RelevanceEntry(string Id, string Lens, string? Key, string Kind, string Label, string? Band)
{
    /// <summary>Every entry of the lenses fact, lens by lens in the fixed lens order, a lens's nodes before its items. An element without a lens item id is not an entry.</summary>
    internal static IReadOnlyList<RelevanceEntry> Of(JsonNode? lenses)
    {
        var entries = new List<RelevanceEntry>();
        foreach (var lens in ScopeRules.Lenses)
        {
            var found = ScopeJson.Obj(lenses, lens);
            foreach (var node in ScopeJson.Items(found, "nodes"))
            {
                if (ScopeJson.Str(node, "lensItem") is { } id)
                {
                    entries.Add(new RelevanceEntry(id, lens, ScopeJson.Str(node, "key"), ScopeJson.Str(node, "kind") ?? "node", ScopeJson.Str(node, "label") ?? ScopeJson.Str(node, "key") ?? id, ScopeJson.Str(node, "band")));
                }
            }

            foreach (var item in ScopeJson.Items(found, "items"))
            {
                if (ScopeJson.Str(item, "lensItem") is { } id)
                {
                    entries.Add(new RelevanceEntry(id, lens, Key: null, ScopeJson.Str(item, "kind") ?? "item", ScopeJson.Str(item, "label") ?? id, Band: null));
                }
            }
        }

        return entries;
    }
}
