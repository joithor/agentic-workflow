using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>Review run 1 (I1): an "every outcome" output (DT spec §8.1) is required by the status line, the 'next' line and the fact row, like the engine's own checks.</summary>
[Lane(TestLane.Unit)]
public sealed class EveryOutputViewsTests
{
    private const string Complete = "sindri workflow complete T-3/step/1 --run 1.1 --outcome <recorded|noneFound> --output fact:evidenceSummary=<file> --output fact:evidenceReason=<file>";

    [Fact]
    public void The_status_commands_and_next_line_name_every_output_a_desk_step_requires_on_every_outcome()
    {
        var view = DeskView("step.evidence.desk", "evidenceSummary");

        Assert.Equal(Complete, view.AllowedCommands.Single(static c => c.Action == "CompleteStep").Cli);
        Assert.Contains(Complete, view.Next, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fact_row_of_such_an_output_is_required_and_checked()
    {
        var view = DeskView("step.evidence.desk", "evidenceSummary");

        var row = view.Facts.Single(static r => r.Key == "fact:evidenceSummary");
        Assert.True(row.Pending!.Required);
        Assert.Equal(["evidenceSummary.present"], row.Pending.Checks);
    }

    /// <summary>Review run 2 (R2-1): 'fact:decision' of a choice Step is the engine's to write, so the desk form, the fact row and the row sentence don't ask the human for it.</summary>
    [Theory]
    [InlineData("step.desk.choose")]
    [InlineData("step.judge.choose")]   // reports [uncertain], so the desk checks read the outcome branch (CallerWritesOn)
    public void An_engine_filled_output_is_not_asked_of_the_human(string step)
    {
        var view = DeskView(step, "decision");

        Assert.DoesNotContain(view.Desk!.Outputs, static o => o.Key == "fact:decision");
        Assert.DoesNotContain("decision.", string.Concat(view.Desk.Checks), StringComparison.Ordinal);
        Assert.False(view.Facts.Single(static r => r.Key == "fact:decision").Pending!.Required);
        Assert.DoesNotContain("decision", RowSentences.Now(view, new StateChip(StateChip.Decision, ChipTone.Attn)), StringComparison.Ordinal);
        Assert.DoesNotContain("--output fact:decision", view.Next, StringComparison.Ordinal);
    }

    private static InstanceView DeskView(string step, string output)
    {
        var recipe = EngineFixture.Step with
        {
            States = new Dictionary<string, RecipeState>(EngineFixture.Step.States, StringComparer.Ordinal)
            {
                ["produce"] = EngineFixture.Step.States["produce"] with { Step = EngineFixture.Step.States["produce"].Step! with { Use = BlockId.From(step) } },
            },
            Outputs = [new RecipeOutput(FactKey.From("fact:" + output), Label: null, Type: null, From: "produce")],
        };
        var interpreted = StatusCases.Recipe with { Recipe = recipe };
        return InstanceViews.Of(EngineFixture.At(recipe, "produce"), interpreted, StatusCases.Now, timeoutAt: null, new InstanceViewContext(Version: 4));
    }
}
