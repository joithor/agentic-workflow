using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Workflow.Engine;
using RecipeDocument = Aw.Sindri.Core.Workflow.Recipes.Recipe;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// PR babysitter spec API18: the commands an instance accepts include each recipe verb ('on: command.&lt;verb&gt;') out of its current state, as the
/// generic 'ApplyCommand:command' with the verb and its CLI line, after the answers and the complete command and before takeover and cancel.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RecipeVerbCommandsTests
{
    private static readonly RecipeDocument _babysitter = EvalComposition.Recipe("sindri/workflow.pr-babysitter.yaml");

    private static InstanceView View(WorkflowInstance instance)
    {
        var interpreted = new InterpretedRecipe(new RecipeVersion(_babysitter.Id, _babysitter.Version, _babysitter.Hash!.Value), _babysitter, EvalComposition.Catalog, new Dictionary<FactKey, InputFact>());   // why: the shipped recipe is read with its hash
        return InstanceViews.Of(instance, interpreted, DateTimeOffset.UnixEpoch, timeoutAt: null, new InstanceViewContext(Version: 1, Children: null));
    }

    [Fact]
    public void The_ready_state_offers_each_of_its_verbs_once_in_recipe_order_between_takeover_and_the_step_commands()
    {
        var view = View(EngineFixture.At(_babysitter, "ready"));
        var address = view.Address.Value;

        Assert.Equal(
            [
                ("ApplyCommand:command", "merge"), ("ApplyCommand:command", "rework"), ("ApplyCommand:command", "comments"),
                ("ApplyCommand:command", "resync-docs"), ("ApplyCommand:command", "check"),
                ("ApplyCommand:takeover", null), ("ApplyCommand:cancel", null),
            ],
            view.AllowedCommands.Select(c => (c.Action, c.Verb)));
        Assert.Equal($"sindri workflow command {address} comments", view.AllowedCommands.Single(c => c.Verb == "comments").Cli);
    }

    [Fact]
    public void A_state_with_no_command_transitions_offers_none()
    {
        var state = _babysitter.States.Keys.First(s => !_babysitter.Transitions.Any(t => t.From.Contains(s, StringComparer.Ordinal) && t.On.StartsWith("command.", StringComparison.Ordinal)));

        var view = View(EngineFixture.At(_babysitter, state));

        Assert.DoesNotContain(view.AllowedCommands, c => c.Action == "ApplyCommand:command");
    }

    [Fact]
    public void A_taken_over_instance_offers_no_recipe_verbs()
    {
        var taken = EngineFixture.At(_babysitter, "ready") with
        {
            Suspension = new Suspension(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, string.Empty, "human:thor", Mirrors: null, new ResumeAction(ResumeKind.Return, Withheld: null, Gate: null, Effects: [])),
        };

        Assert.DoesNotContain(View(taken).AllowedCommands, c => c.Action == "ApplyCommand:command");
    }

    [Fact]
    public void An_ended_instance_offers_none()
    {
        var ended = EngineFixture.At(_babysitter, "ready") with { Frame = new Frame("merged", Entry: 7, Run: null, Child: null), Outcome = OutcomeName.From("resolved") };

        Assert.Empty(View(ended).AllowedCommands);
    }

    [Fact]
    public void The_json_omits_verb_where_there_is_none_and_names_it_where_there_is()
    {
        var options = AwJson.CreateOptions(SindriJsonContext.Default);

        var entries = View(EngineFixture.At(_babysitter, "ready")).AllowedCommands.Select(c => JsonSerializer.SerializeToElement(c, options)).ToList();

        Assert.Equal("merge", entries[0].GetProperty("verb").GetString());
        Assert.False(entries[^1].TryGetProperty("verb", out _));   // cancel
    }

    [Fact]
    public void The_status_text_and_record_keep_their_closed_verbs_and_leave_the_recipe_verbs_to_allowed_commands()
    {
        var view = View(EngineFixture.At(_babysitter, "ready"));

        Assert.Equal("takeover, cancel", StatusLayout.Commands(view));
        Assert.Equal([ActionId.From("ApplyCommand:takeover"), ActionId.From("ApplyCommand:cancel")], StatusRecords.ActionIds(view));
    }
}
