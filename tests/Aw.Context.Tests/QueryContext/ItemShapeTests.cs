using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Tests.Map;
using static Aw.Context.Tests.SyncSources.ExtractDecisionsTests;

namespace Aw.Context.Tests.QueryContext;

/// <summary>
/// The item shape (S-5B, spec §8.1, Phase 5): a tracker key or record key anchors the query on the record its sync linked, the nodes it touches
/// and the decisions it holds, read through the map with the intent layer merged in.
/// </summary>
[Trait("Lane", "integration")]
public sealed class ItemShapeTests
{
    private const string Orientation = "feature:layout.split-pane.orientation";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ContextQuery Item(string key) => new(Text: null, Item: key);

    private static ResultSection Section(ContextResult result, string name) => result.Sections.Single(s => s.Name == name);

    private static async Task<Rig> Synced()
    {
        var rig = await Rig.Start();
        _ = await rig.Sync(Scope());
        return rig;
    }

    [Fact]
    public async Task Item_tracker_key_resolves_through_record_alias()
    {
        await using var rig = await Synced();

        var result = await rig.World.Engine.Query(Item("SPL-103"), MapWorld.Human, Ct);

        Assert.Contains("record:" + R103, result.Value.Anchors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Item_canonical_record_key_is_accepted()
    {
        await using var rig = await Synced();

        var result = await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct);

        Assert.Contains("record:" + R103, result.Value.Anchors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Item_anchors_are_mentions_targets_and_decides_targets_of_its_decisions()
    {
        await using var rig = await Synced();

        var anchors = (await rig.World.Engine.Query(Item("SPL-103"), MapWorld.Human, Ct)).Value.Anchors;

        Assert.Equal("record:" + R103, anchors[0]);   // the record first, then what it touches
        Assert.Contains(Toggle, anchors, StringComparer.Ordinal);
        Assert.Contains(Orientation, anchors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Item_answers_with_record_first_in_records_and_its_decisions_quoted()
    {
        await using var rig = await Synced();

        var result = (await rig.World.Engine.Query(Item("SPL-103"), MapWorld.Human, Ct)).Value;

        var decision = Assert.Single(Section(result, "decisions").Items, i => i.Text!.Contains(Q103, StringComparison.Ordinal));
        Assert.Equal((Provenance.Model, false, Trust.Untrusted), (decision.Provenance, decision.Confirmed, decision.Trust));
        var cite = Assert.Single(result.Cites.Values, c => c.Key.Value == R103);
        Assert.Contains("SPL-103", cite.Aliases, StringComparer.Ordinal);
        Assert.Equal(CitationState.Unchecked, cite.State);   // Onyx can't check versions (S-5A)
        Assert.Equal("2026-07-23T09:00:00.0000000Z", cite.Refs[0].Version.Value);
        Assert.Contains(Section(result, "records").Items, i => i.Title?.Contains("Toggle split orientation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Item_unknown_is_003_with_suggestedAnchors()
    {
        await using var rig = await Synced();

        var result = await rig.World.Engine.Query(Item("SPL-999"), MapWorld.Human, Ct);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, result.Error.Code);
        var suggested = Assert.Single(result.Error.Extensions!, m => m.Key == "suggestedAnchors").Value.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("SPL-103", suggested, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Item_not_synced_falls_back_to_onyx_search_by_key_for_human_callers()
    {
        await using var rig = await Rig.Start();

        var result = (await rig.World.Engine.Query(Item("SPL-103"), MapWorld.Human, Ct)).Value;

        Assert.Empty(result.Anchors);
        Assert.Contains(result.Cites.Values, c => c.Key.Value == R103 && c.Aliases.Contains("SPL-103", StringComparer.Ordinal));
    }

    [Fact]
    public async Task Item_for_session_sees_only_records_linked_to_in_scope_nodes()
    {
        await using var rig = await Synced();
        var session = MapWorld.Session("aw-split-panes");

        var linked = await rig.World.Engine.Query(Item("SPL-103"), session, Ct);
        var outside = await rig.World.Engine.Query(Item("SPL-104"), session, Ct);

        Assert.Contains("record:" + R103, linked.Value.Anchors, StringComparer.Ordinal);   // it mentions a node this repo implements
        Assert.Equal(ContextErrors.UnknownAnchor.Code, outside.Error.Code);   // it touches nothing in scope: not shown, and no fallback to Onyx
    }

    [Fact]
    public async Task Item_with_onyx_down_degrades_010_and_keeps_stored_links()
    {
        var rig = await Synced();
        await using var disposing = rig;
        await rig.Onyx.DisposeAsync();

        var result = (await rig.World.Engine.Query(Item("SPL-103"), MapWorld.Human, Ct)).Value;

        Assert.Contains(result.Degraded, d => d.Source.Value == "onyx" && d.Code == ContextErrors.SourceUnavailable.Code);
        Assert.Contains("record:" + R103, result.Anchors, StringComparer.Ordinal);
        Assert.Contains(Section(result, "decisions").Items, i => i.Text!.Contains(Q103, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_item_is_a_query_error(string item)
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(Item(item), MapWorld.Human, Ct);

        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_contradicting_pair_of_decisions_on_a_reached_node_shows_in_conflicts()
    {
        await using var rig = await Synced();   // 103 and 104 decide the toggle command and the model said they contradict

        var result = (await rig.World.Engine.Query(Item("SPL-103"), MapWorld.Human, Ct)).Value;

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal(Toggle, conflict.Node);
        Assert.Equal(2, conflict.Intent.Count);
    }
}
