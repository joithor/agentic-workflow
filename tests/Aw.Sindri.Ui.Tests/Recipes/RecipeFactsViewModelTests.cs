using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Recipes;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The recipe's declared inputs and outputs (screens spec §4.3, U10a): in declared order, every word a served field.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeFactsViewModelTests
{
    private static JsonNode Golden(string id) => GoldenFiles.Node("workflow-recipe", id);

    private static string Text(JsonNode? node) => (string)node!;

    private static RecipeFactsViewModel Facts(string id) => new(FixtureInstances.Recipe(id));

    [Theory]
    [InlineData("workflow.dev-triage-three-state")]
    [InlineData("workflow.project")]
    [InlineData("workflow.no-outputs")]
    public void Inputs_in_declared_order_with_type_kind_required(string id)
    {
        var served = Golden(id)["inputs"]!.AsArray();
        var facts = Facts(id);

        Expect.Sequence(served.Select(i => Text(i!["key"])), facts.Inputs.Select(i => i.Detail.Split(" · ")[0]));
        Expect.Sequence(
            served.Select(i => $"{Text(i!["key"])} · {Text(i["type"])} · {Text(i["kind"])} · {(i["required"]!.GetValue<bool>() ? "required" : "optional")}"),
            facts.Inputs.Select(i => i.Detail));
    }

    [Fact]
    public void Headings_say_what_the_workflow_needs_and_produces()
    {
        var facts = Facts("workflow.no-outputs");

        Assert.Equal(("What it needs", "What it produces", "This workflow doesn't produce anything."), (facts.InputsHeading, facts.OutputsHeading, facts.NoOutputsText));
    }

    [Fact]
    public void Null_label_reads_the_key()
    {
        var served = Golden("workflow.no-outputs")["inputs"]!.AsArray().Single(i => i!["label"] is null)!;
        var row = Facts("workflow.no-outputs").Inputs.Single(i => i.Detail.StartsWith(Text(served["key"]), StringComparison.Ordinal));

        Assert.Equal(WorkflowNames.InputName(Text(served["key"])), row.Label);   // the key in plain words; the key itself is in the detail line
    }

    [Fact]
    public void Generate_shows_when_served()
    {
        var facts = Facts("workflow.no-outputs");
        var template = Text(Golden("workflow.no-outputs")["inputs"]!.AsArray().Single(i => i!["generate"] is not null)!["generate"]);

        var generated = facts.Inputs.Single(i => i.HasGenerate);
        Assert.Equal(Labels.GenerateLine(template), generated.Generate);
        Assert.Contains(facts.Details, line => string.Equals(line, generated.Generate, StringComparison.Ordinal));   // in Details, not the row
        Assert.Equal(1, facts.Inputs.Count(i => i.HasGenerate));
    }

    [Fact]
    public void Unknown_type_reads_payload()
    {
        var body = (JsonObject)Golden("workflow.no-outputs");
        body["inputs"]![0]!["type"] = "hologram";

        var facts = new RecipeFactsViewModel(FixtureInstances.Parse<RecipeView>(body));

        Assert.Contains(" · payload · ", facts.Inputs[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Outputs_in_declared_order_with_type_and_source()
    {
        var served = Golden("workflow.project")["outputs"]!.AsArray();
        var facts = Facts("workflow.project");

        Assert.True(facts.HasOutputs);
        Expect.Sequence(served.Select(o => $"{Text(o!["key"])} · {Text(o["type"])} · from {Text(o["from"])}"), facts.Outputs.Select(o => o.Detail));
        Expect.Sequence(served.Select(o => Text(o!["label"])), facts.Outputs.Select(o => o.Label));
    }

    [Theory]
    [InlineData("workflow.no-outputs")]
    [InlineData("workflow.item")]
    public void No_outputs_says_so(string id)
    {
        var facts = Facts(id);

        Assert.Equal((false, Labels.NoOutputs), (facts.HasOutputs, facts.NoOutputsText));
        Assert.Empty(facts.Outputs);
    }
}
