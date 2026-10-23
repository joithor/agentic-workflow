using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// PB spec API18: the detail view of an instance lists the recipe verbs it accepts now in 'allowedCommands', one '{ actionId, verb, cli }' per verb in the view's order, so the app can show a
/// button per verb; a state that takes none writes '[]' (the field is always there), and the engine's other commands (takeover, cancel) are not in it.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class InstanceDetailAllowedCommandsTests
{
    private const string Address = "T-3/pr-babysitter/1";

    [Fact]
    public void The_babysitter_at_ready_lists_its_five_verbs_in_the_views_order()
    {
        var view = View("ready");

        var detail = InstanceViews.Detail(view, Context());

        Assert.Equal(["merge", "rework", "comments", "resync-docs", "check"], detail.AllowedCommands.Select(static c => c.Verb), StringComparer.Ordinal);
        Assert.All(detail.AllowedCommands, static c => Assert.Equal("ApplyCommand:command", c.Action));
        Assert.Equal($"sindri workflow command {Address} merge", detail.AllowedCommands[0].Cli);
        Assert.Equal([.. view.AllowedCommands.Where(static c => c.Verb is not null)], detail.AllowedCommands);
    }

    [Fact]
    public void Only_the_verb_with_a_human_trigger_effect_is_destructive_in_the_detail()
    {
        var detail = InstanceViews.Detail(View("ready"), Context());

        Assert.Equal(
            [("merge", true), ("rework", null), ("comments", null), ("resync-docs", null), ("check", null)],
            detail.AllowedCommands.Select(static c => (c.Verb, c.Destructive)));
    }

    [Fact]
    public void The_detail_json_writes_destructive_on_merge_only()
    {
        var json = JsonSerializer.Serialize(InstanceViews.Detail(View("ready"), Context()), EvalComposition.Options);

        var entries = JsonDocument.Parse(json).RootElement.GetProperty("allowedCommands").EnumerateArray().ToList();

        Assert.Equal(["merge"], entries.Where(static e => e.TryGetProperty("destructive", out var flag) && flag.GetBoolean()).Select(static e => e.GetProperty("verb").GetString()), StringComparer.Ordinal);
        Assert.Equal(1, entries.Count(static e => e.TryGetProperty("destructive", out _)));
    }

    [Fact]
    public void The_engines_own_commands_are_not_in_the_list()
    {
        var view = View("ready");

        var detail = InstanceViews.Detail(view, Context());

        Assert.Contains(view.AllowedCommands, static c => c.Verb is null);
        Assert.DoesNotContain(detail.AllowedCommands, static c => c.Verb is null);
    }

    [Fact]
    public void A_state_that_takes_no_verb_writes_an_empty_list_not_a_missing_field()
    {
        var detail = InstanceViews.Detail(View("snapshot"), Context());

        Assert.Empty(detail.AllowedCommands);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(JsonSerializer.Serialize(detail, EvalComposition.Options)).RootElement.GetProperty("allowedCommands").ValueKind);
    }

    [Fact]
    public void An_entry_serializes_as_action_id_verb_and_cli()
    {
        var json = JsonSerializer.Serialize(InstanceViews.Detail(View("ready"), Context()), EvalComposition.Options);

        var first = JsonDocument.Parse(json).RootElement.GetProperty("allowedCommands")[0];

        Assert.Equal("ApplyCommand:command", first.GetProperty("actionId").GetString());
        Assert.Equal("merge", first.GetProperty("verb").GetString());
        Assert.StartsWith("sindri workflow command ", first.GetProperty("cli").GetString(), StringComparison.Ordinal);
    }

    private static InstanceView View(string state)
    {
        var recipe = EvalComposition.Recipe("sindri/workflow.pr-babysitter.yaml");
        var interpreted = new InterpretedRecipe(new RecipeVersion(recipe.Id, recipe.Version, recipe.Hash!.Value), recipe, EvalComposition.Catalog, new Dictionary<FactKey, InputFact>());   // why: the shipped recipe is read with it
        return InstanceViews.Of(EngineFixture.At(recipe, state), interpreted, DateTimeOffset.UnixEpoch, timeoutAt: null, new InstanceViewContext(Version: 1, Children: null));
    }

    private static InstanceDetailContext Context() => new("PR babysitter", Parent: null, new ViewCounts(0, 0, 0, Context: null), DateTimeOffset.UnixEpoch);
}
