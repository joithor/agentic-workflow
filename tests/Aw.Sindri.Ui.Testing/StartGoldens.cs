using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The Start form's goldens read for a test: the request as the API pins it, and the start refusals it serves.</summary>
internal static class StartGoldens
{
    /// <summary>The scenarios of workflow-instances that are start refusals (start-*).</summary>
    internal static IEnumerable<string> Refusals =>
        GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == "workflow-instances" && g.Scenario.StartsWith("start-", StringComparison.Ordinal)).Select(g => g.Scenario);

    /// <summary>The request golden as a record: what the app has to put on the wire for these inputs.</summary>
    /// <returns>The request.</returns>
    internal static StartInstanceRequest Request()
    {
        var json = GoldenFiles.Node("workflow-instances-start", "request");
        var facts = json["facts"]!.AsObject().ToDictionary(f => f.Key, f => Fact(f.Value!), StringComparer.Ordinal); // why: the golden has facts
        return new((string)json["recipe"]!, (string)json["item"]!, (bool)json["nextAttempt"]!, facts); // why: the golden names all three
    }

    private static StartFact Fact(JsonNode fact) => new((string)fact["name"]!, Convert.FromBase64String((string)fact["content"]!)); // why: every fact has both
}
