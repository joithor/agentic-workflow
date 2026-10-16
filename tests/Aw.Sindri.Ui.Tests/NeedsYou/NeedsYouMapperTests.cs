using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The served rows onto the app's rows (T1): the goldens decide the fields, the catalog decides what a button may do.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouMapperTests
{
    private const string Dec = "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2d701";
    private static readonly HomeCounts _counts = new(Running: 2, WithYou: 1, Queued: 3);

    private static T Golden<T>(string slug, string scenario, bool api = false)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(api ? GoldenFiles.Api(slug, scenario) : GoldenFiles.SpecShaped(slug, scenario)), SindriApiClient.Json).Value;

    private static CommandList Catalog() => Golden<CommandList>("commands", "catalog", api: true);

    private static NeedsYouFeed Feed(string scenario = "fixture", bool withDecisions = true) =>
        NeedsYouMapper.Map(Golden<NeedsYouList>("needs-you", scenario), Catalog(), withDecisions ? Golden<DecisionList>("decisions", "pending") : null, _counts);

    [Fact]
    public void Rows_keep_the_apis_order_and_ids() =>
        Assert.Equal(["decision:" + Dec, "failure:service:index", "profile:drift"], Feed().Rows.Select(r => r.Id), StringComparer.Ordinal);

    [Fact]
    public void A_decision_row_carries_its_chip_item_repo_subject_cli_and_notice()
    {
        var row = Feed().Rows[0];

        Assert.Equal((NeedsYouUrgency.Pulse, new NeedsYouChip("DECISION", NeedsYouChipTone.Attn)), (row.Urgency, row.Chip));
        Assert.Equal(("Re-scope: drop the iOS head from this item", "sindri-app", "decision · re-scope"), (row.Title, row.Repo, row.Reason));
        Assert.Equal(("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", Dec, "Decide"), (row.ItemId, row.ShownId, row.PrimaryAction));
        Assert.Equal(new NeedsYouSubject("decision", Dec), row.Subject);
        Assert.Equal(("sindri decide " + Dec + " <choice>", true, "Recorded now; the agent acts on it when it next runs."), (row.Cli, row.RecordedOnly, row.Notice));
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), row.Since);
    }

    [Fact]
    public void A_decision_rows_Decide_becomes_one_action_per_option_with_the_catalogs_readiness()
    {
        var actions = Feed().Rows[0].Actions;

        Assert.Equal(["Accept", "Decline"], actions.Select(a => a.Label), StringComparer.Ordinal);
        Assert.Equal(["accept", "decline"], actions.Select(a => a.OptionId), StringComparer.Ordinal);
        Assert.Equal([false, true], actions.Select(a => a.RejectLike));
        Assert.All(actions, a => Assert.Equal(("Decide", ActionReadiness.Pending), (a.CatalogId, a.Readiness)));   // the golden catalog still lists Decide as pending:P4
        Assert.Equal("sindri decide " + Dec + " decline", actions[1].Cli);
    }

    [Fact]
    public void A_Decide_marked_ready_by_the_catalog_is_ready_on_every_option()
    {
        var ready = Catalog() with { Rows = [.. Catalog().Rows.Select(r => r.Action == "Decide" ? r with { Ui = "ready" } : r)] };

        var actions = NeedsYouMapper.Map(Golden<NeedsYouList>("needs-you", "fixture"), ready, Golden<DecisionList>("decisions", "pending"), _counts).Rows[0].Actions;

        Assert.All(actions, a => Assert.True(a.IsReady));
    }

    [Fact]
    public void Without_the_decisions_list_a_decision_row_keeps_its_one_served_action()
    {
        var action = Assert.Single(Feed(withDecisions: false).Rows[0].Actions);

        Assert.Equal(("Decide", "", "sindri decide " + Dec + " <choice>"), (action.CatalogId, action.OptionId, action.Cli));
    }

    [Fact]
    public void Failure_actions_are_pending_and_a_profile_approve_is_cli_only_from_the_catalog()
    {
        var rows = Feed().Rows;

        Assert.Equal(["RunService", "PauseService"], rows[1].Actions.Select(a => a.CatalogId), StringComparer.Ordinal);
        Assert.All(rows[1].Actions, a => Assert.Equal(ActionReadiness.Pending, a.Readiness));
        Assert.Equal((NeedsYouUrgency.Colour, new NeedsYouChip("FAILED", NeedsYouChipTone.Fail), "sindri services run index"), (rows[1].Urgency, rows[1].Chip, rows[1].Cli));
        Assert.Equal((NeedsYouUrgency.Calm, ActionReadiness.CliOnly, "ApproveProfile"), (rows[2].Urgency, rows[2].Actions[0].Readiness, rows[2].PrimaryAction));
    }

    [Fact]
    public void A_profile_subject_maps_and_a_null_one_stays_null()
    {
        Assert.Equal(new NeedsYouSubject("profile", "3f1c09"), Feed().Rows[2].Subject);
        Assert.Null(Feed("extra-profile-null-subject").Rows[2].Subject);
    }

    [Fact]
    public void An_action_the_catalog_doesnt_list_is_pending_never_runnable()
    {
        var empty = Catalog() with { Rows = [] };

        var rows = NeedsYouMapper.Map(Golden<NeedsYouList>("needs-you", "fixture"), empty, decisions: null, _counts).Rows;

        Assert.All(rows.SelectMany(r => r.Actions), a => Assert.Equal(ActionReadiness.Pending, a.Readiness));
    }

    [Fact]
    public void The_served_chip_wins_over_the_one_the_app_derives()
    {
        var list = Golden<NeedsYouList>("needs-you", "one-request");
        var served = list with { Rows = [list.Rows[0] with { Chip = new StateChip("WITH YOU", ChipTone.Ok) }] };

        var row = NeedsYouMapper.Map(served, Catalog(), decisions: null, _counts).Rows[0];

        Assert.Equal(new NeedsYouChip("WITH YOU", NeedsYouChipTone.Ok), row.Chip);
    }

    [Fact]
    public void An_urgency_the_app_doesnt_know_still_shows_as_a_pulse()
    {
        var list = Golden<NeedsYouList>("needs-you", "one-request");

        var row = NeedsYouMapper.Map(list with { Rows = [list.Rows[0] with { Urgency = "shouting" }] }, Catalog(), decisions: null, _counts).Rows[0];

        Assert.Equal(NeedsYouUrgency.Pulse, row.Urgency);
    }

    [Fact]
    public void The_feed_carries_the_served_sentence_the_views_state_and_the_counts()
    {
        var empty = Feed("empty");
        var stalled = Feed("stalled");
        var rebuilding = Feed("rebuilding");

        Assert.Equal(("Nothing is waiting on you. 2 running, 3 queued.", false, false), (empty.Sentence, empty.Stalled, empty.Rebuilding));
        Assert.Empty(empty.Rows);
        Assert.Equal((true, false), (stalled.Stalled, stalled.Rebuilding));
        Assert.Equal((false, true), (rebuilding.Stalled, rebuilding.Rebuilding));
        Assert.Equal(_counts, stalled.Counts);
    }
}
