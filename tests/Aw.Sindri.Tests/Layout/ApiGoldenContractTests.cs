using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Layout;

/// <summary>
/// The engine's output against the API lane's workflow-* goldens, field for field: InstanceViews.Of then Detail for the dev recipes
/// must serialize to the golden's names, types, nulls and ids. A value is compared too, except what only the fixture authored
/// (the fixture-authored paths: wording, hashes of invented files, counts the ledger supplies).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ApiGoldenContractTests
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);

    /// <summary>Paths whose value the fixture authored and no engine input determines: compared for presence and JSON type only.</summary>
    private static readonly string[] _fixtureAuthored =
    [
        "current.step.declaration", "current.gates", "current.desk.checks", "current.status.next", "current.status.text",
        "current.status.inputs[].hash", "current.status.inputs[].name", "current.status.timeout.inSeconds", "current.status.budget",
        "facts[].summary", "subject.key", "current.status.commands[].cli", "facts[].ref", "facts[].pending.checks", "subject.label", "title", "counts", "states[].round",
    ];

    private static readonly GoldenShapeDiff _diff = new(_fixtureAuthored);

    private static JsonNode GoldenOf(string scenario) =>
        JsonNode.Parse(File.ReadAllText(Golden.SourcePath(typeof(ApiGoldenContractTests).Assembly, $"Golden/Api/workflow-instance/{scenario}.json")))!;

    [Theory]
    [InlineData("dev-desk-round-trip", "desk-round-trip.yaml", "export", "ITM-01JL1", "Desk round trip", "fact:request", "Summarize the week")]
    [InlineData("dev-triage-three-state", "triage-three-state.yaml", "desk", "ITM-01JL2", "Three-state triage", "fact:item", "ITM-01JL2 Crash on launch")]
    public void The_detail_of_a_dev_recipe_matches_its_api_golden_field_for_field(string scenario, string file, string state, string item, string title, string fact, string text)
    {
        var (_, detail) = DevInstances.Produce(file, state, item, title, fact, text);

        var actual = JsonNode.Parse(JsonSerializer.Serialize(detail, _options));
        var differences = _diff.Between(GoldenOf(scenario), actual);

        Assert.True(differences.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, differences));
    }
}
