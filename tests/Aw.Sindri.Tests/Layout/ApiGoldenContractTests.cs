using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Layout;

/// <summary>
/// The engine's output against the API lane's workflow-* goldens, field for field: InstanceViews.Of then Detail for the dev recipes
/// must serialize to the golden's names, types, nulls and values, except the three wording leaves.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ApiGoldenContractTests
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);

    /// <summary>
    /// The three wording leaves, compared for presence and JSON type only: the Step declaration and a gate's description are the
    /// recipe author's words, and a fact's summary is what the producer chose to show. Everything else is compared by value,
    /// every array by length.
    /// </summary>
    private static readonly string[] _wording = ["current.step.declaration", "current.gates[].description", "facts[].summary"];

    private static readonly GoldenShapeDiff _diff = new(_wording);

    private static JsonNode GoldenOf(string scenario) =>
        JsonNode.Parse(File.ReadAllText(Golden.SourcePath(typeof(ApiGoldenContractTests).Assembly, $"Golden/Api/workflow-instance/{scenario}.json")))!;

    [Theory]
    [InlineData("dev-desk-round-trip", "desk-round-trip.yaml", "export", "ITM-01JL1", "Desk round trip", "fact:request", "Summarize the week", "request.txt")]
    [InlineData("dev-triage-three-state", "triage-three-state.yaml", "desk", "ITM-01JL2", "Three-state triage", "fact:item", "ITM-01JL2 Crash on launch", "item.json")]
    public void The_detail_of_a_dev_recipe_matches_its_api_golden_field_for_field(string scenario, string file, string state, string item, string title, string fact, string text, string inputName)
    {
        var (_, detail) = DevInstances.Produce(file, state, item, title, fact, text, inputName);

        var actual = JsonNode.Parse(JsonSerializer.Serialize(detail, _options));
        var differences = _diff.Between(GoldenOf(scenario), actual);

        Assert.True(differences.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, differences));
    }
}
