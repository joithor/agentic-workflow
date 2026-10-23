using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Microsoft.Extensions.Time.Testing;
using Labels = Aw.Sindri.Ui.ViewModels.Labels;
using StateGlyph = Aw.Sindri.Ui.Api.Workflow.StateGlyph;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>The engine's recipe fields on the run canvas (F5 A fields): transitions drawn as labelled edges, per-stage times, who does a state, and the facts a stage reads and writes by key.</summary>
[Lane(TestLane.Unit)]
public sealed class RunRecipeFieldsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Body<T>(T value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value, SindriApiClient.Json));

    private static RecipeTransitionView Move(string from, string to, string? label = null, string outcome = "done") => new([from], "step.completed", to, outcome, label);

    private static RunGraph Graph(RecipeView recipe) => RunGraphBuilder.Build(RunFixtures.Line("plan"), recipe, new Dictionary<string, InstanceDetail>(StringComparer.Ordinal));

    private async Task<RunPageViewModel> Opened(InstanceDetail line, RecipeView recipe)
    {
        var child = RunFixtures.Child("plan", ("produce", StateGlyph.Active, false), ("check", StateGlyph.Pending, false));
        _server.InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line)
            : string.Equals(id, "WFI-plan", StringComparison.Ordinal) ? Body(child)
            : Body(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false)));
        _server.RecipeBody = _ => Body(recipe);
        var page = new RunPageViewModel(_server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await page.Open(_api, line.Address, line.Id, Token);
        return page;
    }

    [Fact]
    public void Served_transitions_are_the_edges_a_way_back_is_a_labelled_loop_and_the_fallback_note_goes()
    {
        var recipe = RunFixtures.LineRecipe() with
        {
            Transitions = [Move("scoping", "design"), Move("design", "plan"), Move("plan", "design", "problems found: design it again", "failed")],
        };

        var graph = Graph(recipe);

        Assert.True(graph.BranchesServed);
        Assert.Equal(["scoping>design", "design>plan", "plan>design"], graph.Edges.Select(e => e.Key), StringComparer.Ordinal);
        Assert.Equal([RunEdgeKind.Main, RunEdgeKind.Main, RunEdgeKind.Loop], graph.Edges.Select(e => e.Kind));
        Assert.Equal("problems found: design it again", graph.Edges[2].Label);
        Assert.Null(graph.Edges[0].Label);
    }

    [Fact]
    public void Two_transitions_between_the_same_cards_are_one_edge_with_the_label_served_for_it()
    {
        var recipe = RunFixtures.LineRecipe() with { Transitions = [Move("scoping", "design"), Move("scoping", "design", "after the questions are answered", "answered")] };

        var edge = Assert.Single(Graph(recipe).Edges);

        Assert.Equal("after the questions are answered", edge.Label);
    }

    [Fact]
    public void A_transition_to_a_card_that_is_not_drawn_adds_nothing()
    {
        var recipe = RunFixtures.LineRecipe() with { Transitions = [Move("scoping", "nowhere")] };

        Assert.Empty(Graph(recipe).Edges);
    }

    [Fact]
    public void A_recipe_with_no_transitions_keeps_the_fallback_chain_and_its_note()
    {
        var graph = Graph(RunFixtures.LineRecipe());

        Assert.False(graph.BranchesServed);
        Assert.NotEmpty(graph.Edges);
    }

    [Fact]
    public async Task A_finished_stage_says_how_long_it_took_and_the_active_one_how_long_it_has_run()
    {
        var line = RunFixtures.Line("plan");
        var timed = line with
        {
            States = [.. line.States.Select(c => c.State switch
            {
                "scoping" => c with { EnteredAt = A.Noon.AddHours(-3), LeftAt = A.Noon.AddHours(-2) },
                "plan" => c with { EnteredAt = A.Noon.AddMinutes(-5) },
                _ => c,
            })],
        };

        var page = await Opened(timed, RunFixtures.LineRecipe());

        Assert.EndsWith(Labels.RunTook(Labels.Duration(3600)), page.Cards.Single(c => c.Id == "scoping").FooterText, StringComparison.Ordinal);
        Assert.EndsWith(Labels.RunRunningFor(Labels.Duration(300)), page.Cards.Single(c => c.Id == "plan").FooterText, StringComparison.Ordinal);
        Assert.DoesNotContain(" · ", page.Cards.Single(c => c.Id == "design").FooterText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_card_names_who_does_its_state_when_the_recipe_says()
    {
        var recipe = RunFixtures.LineRecipe();
        recipe = recipe with { States = [.. recipe.States.Select(s => string.Equals(s.State, "design", StringComparison.Ordinal) ? s with { Kind = RecipeStateKind.SubWorkflow } : s)] };

        var page = await Opened(RunFixtures.Line("plan"), recipe);

        var design = page.Cards.Single(c => c.Id == "design");
        Assert.Equal("Sub-workflow", design.KindWord);
        Assert.EndsWith(", Sub-workflow", design.AutomationName, StringComparison.Ordinal);
        Assert.Equal(string.Empty, page.Cards.Single(c => c.Id == "scoping").KindWord);
    }

    [Fact]
    public async Task The_inspector_lists_what_the_stage_reads_and_writes_by_key_under_the_recipes_labels()
    {
        var recipe = RunFixtures.LineRecipe();
        recipe = recipe with
        {
            States = [.. recipe.States.Select(s => string.Equals(s.State, "design", StringComparison.Ordinal)
                ? s with { Inputs = [new RecipeFactView("fact:project", "The project")], Outputs = [new RecipeFactView("fact:never", "Never made")] }
                : s)],
        };
        var page = await Opened(RunFixtures.Line("plan"), recipe);

        page.Select("design/produce");

        var inspector = page.Inspector!;
        var need = Assert.Single(inspector.Needs);
        Assert.Equal(("The project", Labels.FactReady), (need.Label, need.Word));
        var made = Assert.Single(inspector.Produced);
        Assert.Equal(("Never made", Labels.FactNotProduced), (made.Label, made.Word));
    }
}
