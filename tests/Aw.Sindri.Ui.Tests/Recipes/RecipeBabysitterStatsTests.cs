using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The pull request babysitter's figures on the recipe sheet (api #778, #788 goldens): typed, worded plainly under the run figures, with a figure that needs more runs saying so.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeBabysitterStatsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private const string Recipe = "workflow.dev-triage-three-state";
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<RecipeSheetViewModel> Opened(string scenario)
    {
        _server.RecipeStatsAnswer = RouteAnswer.Api("workflow-recipes-id-stats", scenario);
        var sheet = new RecipeSheetViewModel(_server);
        await sheet.Open(_api, Recipe, Token);
        return sheet;
    }

    private static IEnumerable<(string Label, string Value)> Lines(RecipeStatGroup group) => group.Lines.Select(l => (l.Label, l.Value));

    [Fact]
    public void Both_goldens_read_into_typed_figures()
    {
        var full = AwJson.Deserialize<RecipeStatsView>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("workflow-recipes-id-stats", "pr-babysitter")), SindriApiClient.Json).Value.Babysitter!;
        var empty = AwJson.Deserialize<RecipeStatsView>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("workflow-recipes-id-stats", "pr-babysitter-empty")), SindriApiClient.Json).Value.Babysitter!;

        Assert.Equal((4, 5400L, 14400L, 88000L, 0.75, 1), (full.Pulls, full.SinceStartToReadySeconds.Median, full.SinceStartToReadySeconds.P90, full.TokensPerPull.P90, full.CiFix.Rate, full.RecordErrors));
        Assert.Equal(["nit", "none"], full.Comments.ByType.Select(t => t.Type), StringComparer.Ordinal);
        Assert.Equal([("merged", 2), ("pending", 1)], full.Docs.ByOutcome.Select(o => (o.Reason, o.N)));
        Assert.Equal((0, null, null, null), (empty.Pulls, empty.SinceStartToMergeSeconds.Median, empty.TokensPerPull.P90, empty.CiFix.Rate));
        Assert.Empty(empty.Comments.ByType);
    }

    [Fact]
    public async Task The_sheet_shows_the_babysitter_below_the_run_figures_in_plain_words()
    {
        var sheet = await Opened("pr-babysitter");

        Assert.True(sheet.HasBabysitter);
        Assert.Equal(
            ["Pull requests", "Review comments", "CI fixes", "Rechecks", "What you did when a pull request was ready", "Docs"],
            sheet.BabysitterGroups.Select(g => g.Heading),
            StringComparer.Ordinal);
        Assert.Equal(
            [
                ("Watched", "4"),
                ("Time to ready", "typical 1h 30m, most under 4h 0m"),
                ("Time to merge", "typical 2h 30m, most under 6h 0m"),
                ("Tokens per pull request", "typical 42,000, most under 88,000"),
                ("", "1 merge record couldn't be read"),
            ],
            Lines(sheet.BabysitterGroups[0]));
    }

    [Fact]
    public async Task Comments_split_held_from_handled_and_list_each_type_raw()
    {
        var comments = (await Opened("pr-babysitter")).BabysitterGroups[1];

        Assert.Equal(
            [("Comments seen", "9"), ("Held for you", "2"), ("Handled without you", "7"), ("nit", "fixed 3, replied 1, reacted 1, held 1"), ("none", "held 1, skipped 2")],
            Lines(comments));
    }

    [Fact]
    public async Task The_other_groups_read_ci_fixes_rechecks_what_you_did_and_docs()
    {
        var groups = (await Opened("pr-babysitter")).BabysitterGroups;

        Assert.Equal([("Attempts", "4"), ("Turned CI green", "3"), ("Still red", "1"), ("Success rate", "75%")], Lines(groups[2]));
        Assert.Equal([("Rechecks run", "2"), ("Cleared it", "1"), ("Did not clear it", "1")], Lines(groups[3]));
        Assert.Equal([("Merged", "3"), ("Sent back for rework", "1"), ("Dealt with comments", "2"), ("Asked for the docs again", "1"), ("Ran a check", "1")], Lines(groups[4]));
        Assert.Equal([("Merged with the pull request", "2"), ("Waited for you", "1"), ("Docs merged", "2"), ("Docs still pending", "1")], Lines(groups[5]));
    }

    [Fact]
    public async Task With_no_pulls_the_figures_that_need_runs_say_so_and_empty_groups_are_left_out()
    {
        var sheet = await Opened("pr-babysitter-empty");

        var only = Assert.Single(sheet.BabysitterGroups);
        Assert.Equal(
            [("Watched", "0"), ("Time to ready", "Not enough runs yet"), ("Time to merge", "Not enough runs yet"), ("Tokens per pull request", "Not enough runs yet")],
            Lines(only));
    }

    [Fact]
    public async Task Another_workflow_has_no_babysitter_section()
    {
        var sheet = await Opened("workflow-item");

        Assert.False(sheet.HasBabysitter);
        Assert.Empty(sheet.BabysitterGroups);
    }

    [Fact]
    public void An_unknown_docs_outcome_and_a_missing_rate_read_as_served()
    {
        var stats = AwJson.Deserialize<RecipeStatsView>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("workflow-recipes-id-stats", "pr-babysitter")), SindriApiClient.Json).Value;
        var babysitter = stats.Babysitter! with
        {
            CiFix = stats.Babysitter.CiFix with { Rate = null },
            Docs = new BabysitterDocsView(0, 0, [new BabysitterDocOutcomeView("doneByHand", 1), new BabysitterDocOutcomeView("shrugged", 2)]),
        };

        var groups = RecipeBabysitterGroups.Of(babysitter);

        Assert.Contains(("Success rate", "Not enough runs yet"), Lines(groups.Single(g => g.Heading == "CI fixes")));
        Assert.Equal([("Done by hand", "1"), ("shrugged", "2")], Lines(groups.Single(g => g.Heading == "Docs")));
    }
}
