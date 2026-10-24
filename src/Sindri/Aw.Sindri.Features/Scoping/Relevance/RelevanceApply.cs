using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Features.Scoping.Relevance;

/// <summary>What 'step.scope.relevance-apply' writes: the filtered lenses and coverage, the relevance record and the map fixes.</summary>
/// <param name="Lenses">The lenses fact without the entries that left.</param>
/// <param name="Coverage">The coverage fact rewritten from those lenses.</param>
/// <param name="Relevance">The relevance fact.</param>
/// <param name="MapFixes">The map fixes fact.</param>
internal sealed record RelevanceApplied(JsonObject Lenses, JsonObject Coverage, JsonObject Relevance, JsonObject MapFixes);

/// <summary>The relevance decision table and the fact rewriting it drives (spec 4.3): pure, no I/O.</summary>
internal static class RelevanceApply
{
    /// <summary>Decides every entry of the lenses from the agent's verdicts and the judge's rulings and rewrites the facts.</summary>
    internal static Result<RelevanceApplied> Apply(JsonNode lenses, JsonNode coverage, JsonNode brief, JsonNode verdicts, JsonNode rulings) =>
        throw new NotSupportedException();
}
