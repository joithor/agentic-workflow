using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The ids a citation can resolve to (SCP-CITE-001): lens items in fact:lenses, source refs in fact:brief.refs, answers in fact:answerLog.</summary>
/// <param name="facts">The facts of the run.</param>
internal sealed class ScopeIds(ScopeFacts facts)
{
    /// <summary>The 'L-&lt;lens&gt;-&lt;n&gt;' ids of every node and item of every lens.</summary>
    internal HashSet<string> Lens => field ??= LensItems(facts["lenses"]);

    /// <summary>The 'R&lt;n&gt;' ids of the brief's refs.</summary>
    internal HashSet<string> Refs => field ??= new HashSet<string>(ScopeJson.Items(facts["brief"], "refs").Select(static r => ScopeJson.Str(r, "id")).OfType<string>(), StringComparer.Ordinal);

    /// <summary>The answer log's ids.</summary>
    internal HashSet<string> Answers => field ??= new HashSet<string>(ScopeJson.Items(facts["answerLog"]).Select(static a => ScopeJson.Str(a, "id")).OfType<string>(), StringComparer.Ordinal);

    /// <summary>Whether the citation resolves to something the facts hold.</summary>
    internal bool Resolves(string citation) => Lens.Contains(citation) || Refs.Contains(citation) || Answers.Contains(citation);

    /// <summary>The lens-item ids of a lenses fact (or lenses.json): node and item ids of the four lens entries.</summary>
    internal static HashSet<string> LensItems(JsonNode? lenses)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lens in ScopeRules.Lenses)
        {
            var entry = (lenses as JsonObject)?[lens];
            ids.UnionWith(ScopeJson.Items(entry, "nodes").Select(static n => ScopeJson.Str(n, "lensItem")).OfType<string>());
            ids.UnionWith(ScopeJson.Items(entry, "items").Select(static n => ScopeJson.Str(n, "lensItem")).OfType<string>());
        }

        return ids;
    }
}
