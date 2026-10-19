using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

internal static class PlanChecks
{
    internal static LineReport Run(JsonNode? design, JsonNode? plan, JsonNode? holdoutSets, bool evidence)
    {
        _ = new PlanContext(design, plan, holdoutSets);
        _ = new LineReport(evidence, []);
        throw new NotSupportedException();
    }
}
