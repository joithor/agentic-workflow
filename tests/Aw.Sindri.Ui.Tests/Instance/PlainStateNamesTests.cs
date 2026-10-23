using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The instance frame's states band and facts say a state in the recipe's words once the recipe serves them, and its id only when nothing is served.</summary>
[Lane(TestLane.Unit)]
public sealed class PlainStateNamesTests
{
    private const string Scoping = "Scope the work";

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string NameOf(string id) => string.Equals(id, "scoping", StringComparison.Ordinal) ? Scoping : id;

    // The project recipe's golden with a display name on its scoping state.
    private static RouteAnswer Named()
    {
        var recipe = GoldenFiles.Node("workflow-recipe", "workflow.project");
        recipe["states"]!.AsArray().Single(s => (string?)s!["id"] == "scoping")!["displayName"] = Scoping;
        return new RouteAnswer(HttpStatusCode.OK, recipe.ToJsonString());
    }

    private async Task<InstanceFrameViewModel> Opened(RouteAnswer? recipe)
    {
        if (recipe is { } served)
        {
            _server.RecipeBody = _ => served;
        }

        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);
        var detail = FixtureInstances.Detail("project-build");
        await frame.Open(FactFixtures.Api, detail.Address, detail.Id, Token);
        return frame;
    }

    private static StateNodeViewModel Scoped(InstanceFrameViewModel frame) => frame.Band!.Nodes.Single(n => n.State == "scoping");

    [Fact]
    public void A_state_node_says_the_recipes_name_in_its_text_and_in_its_spoken_name()
    {
        var detail = FixtureInstances.Detail("project-build");

        var band = new StatesBandViewModel(detail.States, detail.OrderedBy, detail.State, NameOf);

        var node = band.Nodes.Single(n => n.State == "scoping");
        Assert.Equal(Scoping, node.StateName);
        Assert.StartsWith(Scoping, node.Text, StringComparison.Ordinal);
        Assert.StartsWith(Scoping, node.Name, StringComparison.Ordinal);
        Assert.DoesNotContain("scoping", node.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void A_state_with_no_served_name_reads_its_id()
    {
        var detail = FixtureInstances.Detail("project-build");

        var band = new StatesBandViewModel(detail.States, detail.OrderedBy, detail.State, NameOf);

        var other = band.Nodes.First(n => n.State != "scoping");
        Assert.Equal(other.State, other.StateName);
    }

    [Fact]
    public async Task The_frame_reads_the_recipe_and_its_band_and_facts_say_the_names()
    {
        var frame = await Opened(Named());

        Assert.Equal(Scoping, Scoped(frame).StateName);
        var from = frame.Facts!.Rows.First(r => r.From.Length > 0 && r.From.Contains(Scoping, StringComparison.Ordinal));
        Assert.Equal(Labels.FromState(Scoping), from.From);
        Assert.DoesNotContain(frame.Facts.Rows, r => string.Equals(r.From, Labels.FromState("scoping"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_recipe_that_serves_no_state_names_leaves_the_ids()
    {
        var frame = await Opened(recipe: null);

        Assert.Equal("scoping", Scoped(frame).StateName);
        Assert.Contains(frame.Facts!.Rows, r => string.Equals(r.From, Labels.FromState("scoping"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_recipe_is_read_once_however_often_the_frame_reads_again()
    {
        var reads = 0;
        _server.RecipeBody = _ =>
        {
            reads++;
            return Named();
        };
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);
        var detail = FixtureInstances.Detail("project-build");
        await frame.Open(FactFixtures.Api, detail.Address, detail.Id, Token);

        await frame.Refresh(FactFixtures.Api, Token);
        await frame.Refresh(FactFixtures.Api, Token);

        Assert.Equal(1, reads);
    }
}
