using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// A new current map links every stored record again (spec §13): the links follow the map that is current, whenever it was published, from the cleaned text each record kept.
/// Offline: no source is searched or fetched.
/// </summary>
[Trait("Lane", "unit")]
public sealed class RelinkOnPublishTests : IDisposable
{
    private const string Setting = "surface:setting/paneResizeStep";
    private static readonly SourceId _onyx = SourceId.From("onyx");
    private static readonly RecordKey _r103 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly RecordKey _r104 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000104");
    private static readonly DateTimeOffset _at = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly ContextScope _scope = MapWorld.HumanWith("""{"sources":{"linear":{"teams":["SPL"]}}}""");

    private readonly MapWorld _world = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _world.Dispose();

    private static IntentRecord Record(RecordKey key, string alias) => new(key, _onyx, "v1", "Title of " + alias, [alias]);

    private static IntentLink Stale(RecordKey record, string target) => new(record, NodeKey.From(target), LinkRule.Identifier, 0.7, "ab12");

    // A record synced before this map: it kept its cleaned text and whatever links the map of that day gave it.
    private async Task Seed(RecordKey key, string alias, string text, params IntentLink[] links)
    {
        var page = new IntentPage(_onyx, [Record(key, alias)], links, Cursor: null, _at) { MapVersion = 1, Texts = new Dictionary<string, string>(StringComparer.Ordinal) { [key.Value] = text } };
        Assert.True((await _world.Intents.CommitPage(page, Ct)).IsSuccess);
    }

    private async Task<MapVersion> Publish(ContextScope? scope = null)
    {
        var preview = await _world.Map.PreviewMap(MapWorld.MapPath, scope ?? MapWorld.Human, Ct);
        var imported = await _world.Map.ImportMap(MapWorld.MapPath, preview.Value.Hash, scope ?? MapWorld.Human, Ct);
        Assert.True(imported.IsSuccess, imported.IsError ? imported.Error.Message : null);
        return imported.Value.Version;
    }

    private async Task<IReadOnlyList<(string Record, string Target)>> Links() =>
        [.. (await _world.Intents.Layer(Ct)).Value.Links.Select(l => (l.Record.Value, l.Target.Value))];

    [Fact]
    public async Task A_record_synced_before_the_map_had_its_node_gains_the_link_when_the_map_is_published()
    {
        await Seed(_r103, "SPL-103", "Raise the paneResizeStep default.");
        Assert.Empty(await Links());

        var version = await Publish();

        Assert.Equal([(_r103.Value, Setting)], await Links());
        Assert.Equal([_r103], (await _world.Intents.LinkedUnder(_onyx, version.Number, Ct)).Value);
    }

    [Fact]
    public async Task A_link_to_a_node_the_map_no_longer_has_is_replaced_by_the_links_of_the_current_map()
    {
        await Seed(_r103, "SPL-103", "Raise the paneResizeStep default.", Stale(_r103, "surface:command/gone"));

        _ = await Publish();

        Assert.Equal([(_r103.Value, Setting)], await Links());
    }

    [Fact]
    public async Task A_link_by_issue_key_to_a_record_not_synced_yet_stays()
    {
        await Seed(_r103, "SPL-103", "Blocked by SPL-105.");

        _ = await Publish(_scope);

        Assert.Equal([(_r103.Value, "record:SPL-105")], await Links());
    }

    [Fact]
    public async Task A_record_that_kept_no_text_keeps_its_links()
    {
        var page = new IntentPage(_onyx, [Record(_r104, "SPL-104")], [Stale(_r104, "surface:command/gone")], Cursor: null, _at) { MapVersion = 1 };
        Assert.True((await _world.Intents.CommitPage(page, Ct)).IsSuccess);

        _ = await Publish();

        Assert.Equal([(_r104.Value, "surface:command/gone")], await Links());
    }

    [Fact]
    public async Task Publishing_again_gives_the_same_links_and_leaves_decisions_and_the_queue_alone()
    {
        await Seed(_r103, "SPL-103", "Raise the paneResizeStep default. Blocked by SPL-105.");
        _ = await Publish(_scope);
        var once = await Links();
        var queued = (await _world.Intents.Queued(_onyx, Ct)).Value;

        _ = await Publish(_scope);

        Assert.Equal(once, await Links());
        Assert.Equal(queued, (await _world.Intents.Queued(_onyx, Ct)).Value);
        Assert.Equal(2, once.Count);
    }

    [Fact]
    public async Task Promoting_a_draft_links_the_stored_records_against_the_promoted_map()
    {
        await Seed(_r103, "SPL-103", "Nothing here names a node.", Stale(_r103, "surface:command/gone"));
        _ = await Drafts.Build(_world);
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), Drafts.Scope, Ct)).IsSuccess);

        var promoted = (await _world.Map.Promote(Drafts.Scope, Ct)).Value;

        Assert.Empty(await Links());
        Assert.Equal([_r103], (await _world.Intents.LinkedUnder(_onyx, promoted.Version.Number, Ct)).Value);
    }
}
