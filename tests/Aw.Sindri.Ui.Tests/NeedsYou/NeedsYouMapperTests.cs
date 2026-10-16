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
    private static readonly System.Text.Json.Nodes.JsonArray _served = GoldenFiles.Node("needs-you", "fixture")["rows"]!.AsArray();
    private static readonly string _dec = (string)_served[0]!["subject"]!["id"]!;
    private static readonly HomeCounts _counts = new(Running: 2, WithYou: 1, Queued: 3);

    // The API's golden; an extra- scenario is the one spec-shaped case no golden covers.
    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(scenario.StartsWith("extra-", StringComparison.Ordinal) ? GoldenFiles.SpecShaped(slug, scenario) : GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    private static CommandList Catalog() => Golden<CommandList>("commands", "catalog");

    // The pending golden lists decisions by their own ids; the fixture row's decision is the first of them under the row's id.
    private static DecisionList Pending()
    {
        var pending = Golden<DecisionList>("decisions", "pending");
        return pending with { Decisions = [pending.Decisions[0] with { Id = _dec }, .. pending.Decisions.Skip(1)] };
    }

    private static NeedsYouFeed Feed(string scenario = "fixture", bool withDecisions = true) =>
        NeedsYouMapper.Map(Golden<NeedsYouList>("needs-you", scenario), Catalog(), withDecisions ? Pending() : null, _counts);

    [Fact]
    public void Rows_keep_the_apis_order_and_ids() =>
        Assert.Equal(_served.Select(r => (string)r!["id"]!), Feed().Rows.Select(r => r.Id), StringComparer.Ordinal);

    [Fact]
    public void A_decision_row_carries_its_chip_item_repo_subject_cli_and_notice()
    {
        var row = Feed().Rows[0];

        Assert.Equal((NeedsYouUrgency.Pulse, new NeedsYouChip("DECISION", NeedsYouChipTone.Attn)), (row.Urgency, row.Chip));
        var served = _served[0]!;
        Assert.Equal(((string)served["title"]!, (string)served["repo"]!, (string)served["label"]!), (row.Title, row.Repo, row.Reason));
        Assert.Equal(((string?)served["item"], _dec, "Decide"), (row.ItemId, row.ShownId, row.PrimaryAction));
        Assert.Equal(new NeedsYouSubject("decision", _dec), row.Subject);
        Assert.Equal(((string)served["cli"]!, true, (string)served["notice"]!), (row.Cli, row.RecordedOnly, row.Notice));
        Assert.Equal(served["since"]!.GetValue<DateTimeOffset>(), row.Since);
    }

    [Fact]
    public void A_decision_rows_Decide_becomes_one_action_per_option_with_the_catalogs_readiness()
    {
        var actions = Feed().Rows[0].Actions;

        Assert.Equal(["Accept", "Decline"], actions.Select(a => a.Label), StringComparer.Ordinal);
        Assert.Equal(["accept", "decline"], actions.Select(a => a.OptionId), StringComparer.Ordinal);
        Assert.Equal([false, true], actions.Select(a => a.RejectLike));
        Assert.All(actions, a => Assert.Equal(("Decide", ActionReadiness.Ready), (a.CatalogId, a.Readiness)));   // P4's catalog lists Decide as ready
        Assert.Equal(Pending().Decisions[0].Options[1].Cli, actions[1].Cli);
    }

    [Fact]
    public void A_Decide_marked_ready_by_the_catalog_is_ready_on_every_option()
    {
        var ready = Catalog() with { Rows = [.. Catalog().Rows.Select(r => r.Action == "Decide" ? r with { Ui = "ready" } : r)] };

        var actions = NeedsYouMapper.Map(Golden<NeedsYouList>("needs-you", "fixture"), ready, Pending(), _counts).Rows[0].Actions;

        Assert.All(actions, a => Assert.True(a.IsReady));
    }

    // Proof B4: the decision sheet shows what the decision asks, as served ("Accept this re-scope request?" and the text).
    [Fact]
    public void A_decision_row_carries_the_served_question()
    {
        var row = Feed().Rows[0];

        Assert.Equal(Pending().Decisions[0].Question, row.Question);
        Assert.Contains("?", row.Question, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_without_a_decision_has_no_question() => Assert.Equal("", Feed(withDecisions: false).Rows[0].Question);

    [Fact]
    public void Without_the_decisions_list_a_decision_row_keeps_its_one_served_action()
    {
        var action = Assert.Single(Feed(withDecisions: false).Rows[0].Actions);

        Assert.Equal(("Decide", "", (string)_served[0]!["cli"]!), (action.CatalogId, action.OptionId, action.Cli));
    }

    [Fact]
    public void Failure_actions_are_pending_and_a_profile_approve_is_cli_only_from_the_catalog()
    {
        var rows = Feed().Rows;

        Assert.Equal(["RunService", "PauseService"], rows[1].Actions.Select(a => a.CatalogId), StringComparer.Ordinal);
        Assert.All(rows[1].Actions, a => Assert.Equal(ActionReadiness.Pending, a.Readiness));
        Assert.Equal((NeedsYouUrgency.Colour, new NeedsYouChip("FAILED", NeedsYouChipTone.Fail), (string)_served[1]!["cli"]!), (rows[1].Urgency, rows[1].Chip, rows[1].Cli));
        Assert.Equal((NeedsYouUrgency.Calm, ActionReadiness.CliOnly, "ApproveProfile"), (rows[2].Urgency, rows[2].Actions[0].Readiness, rows[2].PrimaryAction));
    }

    [Fact]
    public void A_profile_subject_maps_and_a_null_one_stays_null()
    {
        Assert.Equal(new NeedsYouSubject("profile", (string)_served[2]!["subject"]!["id"]!), Feed().Rows[2].Subject);
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

        Assert.Equal(((string)GoldenFiles.Node("needs-you", "empty")["empty"]!, false, false), (empty.Sentence, empty.Stalled, empty.Rebuilding));
        Assert.Empty(empty.Rows);
        Assert.Equal((true, false), (stalled.Stalled, stalled.Rebuilding));
        Assert.Equal((false, true), (rebuilding.Stalled, rebuilding.Rebuilding));
        Assert.Equal(_counts, stalled.Counts);
    }

    // review run 1, K5: the served reason sentence reaches the sheets (the label is the row's secondary line).
    [Fact]
    public void The_failure_and_profile_sheets_show_the_served_reason_from_the_golden()
    {
        var rows = Feed().Rows;
        var failure = rows.First(r => r.Subject?.Kind == "service");
        var profile = rows.First(r => r.Id == "profile:drift");
        var servedFailure = (string)_served.First(r => (string)r!["id"]! == failure.Id)!["reason"]!;
        var servedProfile = (string)_served.First(r => (string)r!["id"]! == profile.Id)!["reason"]!;

        Assert.Contains(new SheetDetail("Why", servedFailure), new FailureSheetViewModel(failure, "1h").Details);
        Assert.Contains(new SheetDetail("Changed", servedProfile), new ProfileSheetViewModel(profile).Details);
    }
}
