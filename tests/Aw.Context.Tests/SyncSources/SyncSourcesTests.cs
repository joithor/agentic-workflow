using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fakes;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.Onyx;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// The SyncSources slice (S-5B, spec §8.8) over the fake Onyx and the fixture map: records linked to nodes by rule, unchanged versions skipped,
/// a cursor that resumes after the time cap, a source that is down reported and its old links kept.
/// </summary>
[Trait("Lane", "integration")]
public sealed class SyncSourcesTests
{
    private const string Spl103 = "linear:issue/0e5a1b00-0000-4000-8000-000000000103";
    private const string Toggle = "surface:command/toggle-orientation";
    private static readonly ContextScope _scope = MapWorld.HumanWith("""{"sources":{"linear":{"teams":["SPL"]}},"labeling":{"enabled":true}}""");
    private static readonly SourceId _onyx = SourceId.From("onyx");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SyncSources_links_fixture_records_to_map_nodes()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);
        _ = await world.ImportFixture();

        var report = await world.Engine.Sync("onyx", _scope, Ct);

        var row = Assert.Single(report.Value.Sources);
        Assert.Equal((_onyx, null), (row.Source, row.Code));
        Assert.True(row.Fetched > 0);
        var layer = (await world.Intents.Layer(Ct)).Value;
        var link = Assert.Single(layer.Links, l => l.Record.Value == Spl103 && l.Target.Value == Toggle);
        Assert.Equal(LinkRule.Identifier, link.Rule);
        Assert.Equal(["SPL-103"], layer.Records.Single(r => r.Key.Value == Spl103).Aliases);
        Assert.Equal(layer.Links.Count, row.Linked);
    }

    [Fact]
    public async Task SyncSources_skips_hits_whose_version_is_unchanged()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);
        _ = await world.ImportFixture();
        var first = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];
        var requests = onyx.Requests.Count;

        var second = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];

        Assert.Equal((0, first.Fetched), (second.Fetched, second.Unchanged));
        Assert.DoesNotContain(onyx.Requests.Skip(requests), r => !r.Path.EndsWith("/search", StringComparison.Ordinal));   // only searches: nothing was fetched again
        Assert.Equal(first.Records, second.Records);
    }

    [Fact]
    public async Task SyncSources_resumes_from_cursor_after_time_cap()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var source = new ScriptedSource(harness.Source);
        using var world = new MapWorld(intent: source);
        _ = await world.ImportFixture();
        var text = new MapText(new FakeScrubberFactory().For([]).Value);
        var total = (await new MapReader(world.Store, world.Intents).ReadWithoutIntent(_scope, draft: false, Ct)).Value.Graph.Nodes.Count(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision) && text.SearchTerms(n.Label).Count > 0);   // a label with no search terms is never searched
        source.AfterSearch = () => world.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));   // every search takes longer than the whole run may

        var capped = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];

        Assert.Equal(ContextErrors.SyncCapped.Code, capped.Code);
        var searchedFirst = source.Searches;
        Assert.Equal(1, searchedFirst);   // the cap is checked before each node: the first search took longer than the run may
        Assert.Equal(1, capped.Fetched);   // that node has several new hits: the cap cut it short after its first fetch, so its cursor stays at the node
        Assert.NotNull((await world.Intents.State(_onyx, Ct)).Value.Cursor);

        source.AfterSearch = null;
        var resumed = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];

        Assert.Null(resumed.Code);
        Assert.Equal(total + 1, source.Searches);   // the second run searched the nodes the first one didn't reach, and the node the cap cut short again
        Assert.Equal(source.Searched[0], source.Searched[1]);   // ... and that node came first
    }

    [Fact]
    public async Task SyncSources_stops_between_fetches_when_the_time_cap_passes_and_the_next_run_finishes_the_node()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var whole = new MapWorld(intent: harness.Source);
        _ = await whole.ImportFixture();
        var all = (await whole.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0].Fetched;
        var source = new ScriptedSource(harness.Source);
        using var world = new MapWorld(intent: source);
        _ = await world.ImportFixture();
        source.AfterFetch = () => world.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));   // the first fetch takes longer than the whole run may

        var capped = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];

        Assert.Equal(ContextErrors.SyncCapped.Code, capped.Code);
        Assert.Equal(1, capped.Fetched);   // the node's other hits waited for the next run
        source.AfterFetch = null;
        var resumed = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];

        Assert.Null(resumed.Code);
        Assert.Equal(all, (await world.Intents.Layer(Ct)).Value.Records.Count);   // nothing lost; the record the cap cut the node after was extracted before the run stopped, so it was not fetched again
        Assert.Equal(0, resumed.Queued);
    }

    [Fact]
    public async Task SyncSources_a_map_change_relinks_every_record_across_runs_even_when_the_first_run_stops_early()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var source = new ScriptedSource(harness.Source);
        using var world = new MapWorld(intent: source);
        _ = await world.ImportFixture();
        _ = await world.Engine.Sync("onyx", _scope, Ct);
        var stored = (await world.Intents.Layer(Ct)).Value.Records.Count;
        _ = await world.ImportYaml(FixtureMap.Yaml.Replace("edges:", "  - key: thing:zzz-added\n    label: \"Added by the new map\"\nedges:", StringComparison.Ordinal));   // a new map version
        source.AfterFetch = () => world.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));   // the first run stops after its first record

        var first = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];
        source.AfterFetch = null;
        var second = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];
        var third = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];

        Assert.Equal(ContextErrors.SyncCapped.Code, first.Code);
        Assert.Null(second.Code);
        Assert.True(first.Fetched + second.Fetched >= stored, "the run that stopped early and the next one must together re-link every stored record; the rest kept their old-map links");
        Assert.Equal((0, null), (third.Fetched, third.Code));   // the relink is done: a later run skips unchanged records again
    }

    [Fact]
    public async Task SyncSources_after_a_finished_run_whose_last_nodes_have_no_terms_the_next_run_walks_the_whole_map_again()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var source = new ScriptedSource(harness.Source);
        using var world = new MapWorld(intent: source);
        _ = await world.ImportYaml(FixtureMap.Yaml.Replace("edges:", "  - key: thing:zzz-termless\n    label: \"[REDACTED:x]\"\nedges:", StringComparison.Ordinal));   // sorts last, has no search terms
        var text = new MapText(new FakeScrubberFactory().For([]).Value);
        var total = (await new MapReader(world.Store, world.Intents).ReadWithoutIntent(_scope, draft: false, Ct)).Value.Graph.Nodes.Count(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision) && text.SearchTerms(n.Label).Count > 0);

        var first = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];
        var searchedFirst = source.Searches;
        var second = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];

        Assert.Null(first.Code);
        Assert.Equal(total, searchedFirst);
        Assert.Null(second.Code);
        Assert.Equal(2 * total, source.Searches);   // a finished run restarts at the first node, not at the termless tail
    }

    [Fact]
    public async Task SyncSources_with_onyx_down_reports_010_and_keeps_previous_links()
    {
        var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);
        _ = await world.ImportFixture();
        _ = await world.Engine.Sync("onyx", _scope, Ct);
        var before = (await world.Intents.Layer(Ct)).Value.Links.Count;
        await onyx.DisposeAsync();

        var down = await world.Engine.Sync("onyx", _scope, Ct);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, down.Value.Sources[0].Code);
        Assert.Equal(before, (await world.Intents.Layer(Ct)).Value.Links.Count);
        Assert.Equal(ContextErrors.SourceUnavailable.Code, (await world.Intents.State(_onyx, Ct)).Value.LastError);
    }

    [Fact]
    public async Task SyncSources_hidden_onyx_doc_is_never_linked()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxPlant = static json => Hide(json, "0e5a1b00-0000-4000-8000-000000000103") });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);
        _ = await world.ImportFixture();

        _ = await world.Engine.Sync("onyx", _scope, Ct);

        var layer = (await world.Intents.Layer(Ct)).Value;
        Assert.DoesNotContain(layer.Records, r => r.Key.Value == Spl103);
        Assert.NotEmpty(layer.Records);
    }

    [Fact]
    public async Task Sync_stores_the_fetched_records_created_at()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions
        {
            OnyxPlant = static json => Regex.Replace(json, "\"created_at\"\\s*:\\s*\"[^\"]*\"", "\"created_at\":\"2026-07-02T09:00:00.000Z\"", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
        });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);
        _ = await world.ImportFixture();

        _ = await world.Engine.Sync("onyx", _scope, Ct);

        var record = (await world.Intents.Layer(Ct)).Value.Records.Single(r => r.Key.Value == Spl103);
        Assert.Equal(new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero), record.CreatedAt);
    }

    [Fact]
    public async Task Sync_with_no_created_at_stores_null()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions
        {
            OnyxPlant = static json => json.Replace("\"created_at\"", "\"created_elsewhere\"", StringComparison.Ordinal),
        });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);
        _ = await world.ImportFixture();

        _ = await world.Engine.Sync("onyx", _scope, Ct);

        var records = (await world.Intents.Layer(Ct)).Value.Records;
        Assert.NotEmpty(records);
        Assert.All(records, r => Assert.Null(r.CreatedAt));
    }

    [Fact]
    public async Task SyncSources_scrubs_title_before_storing()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions
        {
            OnyxPlant = static json => json.Replace("[SPL-103] Toggle split orientation", "[SPL-103] Toggle FAKE-SECRET-1234 orientation", StringComparison.Ordinal),
        });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);
        _ = await world.ImportFixture();

        _ = await world.Engine.Sync("onyx", _scope, Ct);

        var title = (await world.Intents.Layer(Ct)).Value.Records.Single(r => r.Key.Value == Spl103).Title;
        Assert.DoesNotContain(FakeScrubber.Secret, title, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:fake]", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SyncSources_without_map_answers_030()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);

        var report = await world.Engine.Sync("onyx", _scope, Ct);

        Assert.Equal(ContextErrors.NoMap.Code, report.Error.Code);
    }

    [Fact]
    public async Task SyncSources_runs_repo_docs_refresh_too()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var world = new MapWorld(intent: harness.Source);
        _ = await world.ImportFixture();

        var report = await world.Engine.Sync(source: null, _scope, Ct);

        Assert.Equal(["repo-docs", "onyx"], report.Value.Sources.Select(s => s.Source.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task SyncSources_unknown_source_answers_058()
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();

        var report = await world.Engine.Sync("linear-native", _scope, Ct);

        Assert.Equal(ContextErrors.SyncSourceUnknown.Code, report.Error.Code);
    }

    [Fact]
    public async Task SyncSources_a_second_sync_of_a_running_source_answers_056()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var source = new ScriptedSource(harness.Source);
        using var world = new MapWorld(intent: source);
        _ = await world.ImportFixture();
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        source.OnSearch = async token =>
        {
            _ = entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System, token);
        };

        var running = world.Engine.Sync("onyx", _scope, Ct).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, Ct);
        var refused = await world.Engine.Sync("onyx", _scope, Ct);
        release.SetResult();

        Assert.Equal(ContextErrors.SyncRunning.Code, refused.Error.Code);
        Assert.True((await running).IsSuccess);
    }

    private static string Hide(string json, string documentId)
    {
        if (JsonNode.Parse(json) is not JsonObject { } reply || reply["documents"] is not JsonArray documents)
        {
            return json;
        }

        foreach (var document in documents.OfType<JsonObject>().Where(d => (string?)d["document_id"] == documentId))
        {
            document["hidden"] = true;
        }

        return reply.ToJsonString();
    }
}
