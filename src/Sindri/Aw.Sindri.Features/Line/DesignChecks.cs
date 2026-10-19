using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

internal static class DesignChecks
{
    internal static LineReport Run(JsonNode? scope, JsonNode? design, JsonNode? lensRounds, JsonNode? accepted, bool evidence)
    {
        _ = new DesignContext(scope, design, lensRounds, accepted);
        _ = new LineFinding("", Item: null, "", "", evidence);
        throw new NotSupportedException();
    }
}
