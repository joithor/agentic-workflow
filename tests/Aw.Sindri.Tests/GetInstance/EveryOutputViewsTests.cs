using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>Review run 1 (I1): an "every outcome" output (DT spec §8.1) is required by the status line, the 'next' line and the fact row, like the engine's own checks.</summary>
[Lane(TestLane.Unit)]
public sealed class EveryOutputViewsTests
{
    private const string Complete = "sindri workflow complete T-3/step/1 --run 1.1 --outcome recorded --output fact:evidenceSummary=<file> --output fact:evidenceReason=<file>";

    [Fact]
    public void The_status_commands_and_next_line_name_every_output_a_desk_step_requires_on_every_outcome()
    {
        var view = DeskView();

        Assert.Equal(Complete, view.AllowedCommands.Single(static c => c.Action == "CompleteStep").Cli);
        Assert.Contains(Complete, view.Next, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fact_row_of_such_an_output_is_required_and_checked()
    {
        var view = DeskView();

        var row = view.Facts.Single(static r => r.Key == "fact:evidenceSummary");
        Assert.True(row.Pending!.Required);
        Assert.Equal(["evidenceSummary.present"], row.Pending.Checks);
    }

    private static InstanceView DeskView()
    {
        var recipe = EngineFixture.Step with
        {
            States = new Dictionary<string, RecipeState>(EngineFixture.Step.States, StringComparer.Ordinal)
            {
                ["produce"] = EngineFixture.Step.States["produce"] with { Step = EngineFixture.Step.States["produce"].Step! with { Use = BlockId.From("step.evidence.desk") } },
            },
            Outputs = [new RecipeOutput(FactKey.From("fact:evidenceSummary"), Label: null, Type: null, From: "produce")],
        };
        var interpreted = StatusCases.Recipe with { Recipe = recipe };
        return InstanceViews.Of(EngineFixture.At(recipe, "produce"), interpreted, StatusCases.Now, timeoutAt: null, new InstanceViewContext(Version: 4));
    }
}
