using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What the gap judge is shown about one candidate (spec 4.5), built from named fields only so nothing of the searcher's argument can reach it.</summary>
internal static class GapJudgeProjection
{
    /// <summary>The question.</summary>
    internal const string Question = "Does the change in the brief need this candidate, and does the scope not cover it already?";

    /// <summary>The three rulings and when to pick each.</summary>
    internal static IReadOnlyList<ChooseOption> Choices { get; } = [];

    /// <summary>The facts one call reads, by key, as text: the brief, the acceptance items, the map's surfaces, the gathered entries and the candidate's title, kind, citation and excerpt.</summary>
    /// <param name="brief">The brief fact.</param>
    /// <param name="acceptance">The acceptance fact.</param>
    /// <param name="scopeMap">The scope map fact.</param>
    /// <param name="lenses">The lenses fact.</param>
    /// <param name="candidate">One candidate of the searcher's fact.</param>
    /// <returns>The facts.</returns>
    internal static IReadOnlyDictionary<string, string> Facts(JsonNode? brief, JsonNode? acceptance, JsonNode? scopeMap, JsonNode? lenses, JsonNode? candidate)
    {
        _ = (brief, acceptance, scopeMap, lenses, candidate);
        throw new NotSupportedException();
    }
}
