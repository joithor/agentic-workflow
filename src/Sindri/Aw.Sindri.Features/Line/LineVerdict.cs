using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>What a line Step's code decided: the outcome and the facts it writes (JSON trees by key, without the 'fact:' prefix).</summary>
/// <param name="Outcome">One of the Step's declared outcomes.</param>
/// <param name="Outputs">The facts written.</param>
internal sealed record LineVerdict(string Outcome, IReadOnlyDictionary<string, JsonNode> Outputs)
{
    /// <summary>An outcome that writes no fact.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The verdict.</returns>
    internal static LineVerdict Of(string outcome) => new(outcome, new Dictionary<string, JsonNode>(StringComparer.Ordinal));

    /// <summary>An outcome that writes the given facts.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="facts">The facts.</param>
    /// <returns>The verdict.</returns>
    internal static LineVerdict Of(string outcome, params (string Key, JsonNode Value)[] facts) =>
        new(outcome, facts.ToDictionary(static f => f.Key, static f => f.Value, StringComparer.Ordinal));
}
