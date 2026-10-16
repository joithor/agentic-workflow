using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fakes;
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
        Assert.InRange(searchedFirst, 1, total - 1);
        Assert.NotNull((await world.Intents.State(_onyx, Ct)).Value.Cursor);

        source.AfterSearch = null;
        var resumed = (await world.Engine.Sync("onyx", _scope, Ct)).Value.Sources[0];

        Assert.Null(resumed.Code);
        Assert.Equal(total, source.Searches);   // the second run searched only the nodes the first one didn't reach
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
