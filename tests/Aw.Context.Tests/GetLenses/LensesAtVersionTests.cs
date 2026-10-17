using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetLenses;

/// <summary>Lenses over an anchor set and as of a retained map version (scoping spec C1, C6, C10, C13).</summary>
public sealed class LensesAtVersionTests : IDisposable
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string Groups = "feature:tabs.tab-groups";
    private const string OnlyInTwo = "feature:only-in-two";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static MapRead V(string text) => MapRead.Of(draft: false, text).Value;

    private ValueTask<Kernel.Result<LensesSetView>> Set(string[] anchors, ContextScope? scope = null, MapRead? read = null) =>
        _world.Engine.LensesOf(anchors, read ?? MapRead.Current, depth: null, scope ?? MapWorld.Human, MapWorld.None);

    private async Task<DateTimeOffset> TabsThenOther()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);   // v1
        var created = _world.Clock.GetUtcNow();
        _world.Clock.Advance(TimeSpan.FromDays(3));
        _ = await _world.ImportYaml(MapWorld.Yaml("schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", $"  - {{key: \"{OnlyInTwo}\", label: \"Two\"}}"));   // v2
        return created;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetLenses_set_reads_one_snapshot_for_all_anchors()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var reads = 0;
        _world.Store.AfterSnapshot = _ => reads++;

        var view = await Set([Manager, Groups]);

        Assert.Equal((2, 1), (view.Value.SchemaVersion, reads));
        Assert.Equal([Manager, Groups], view.Value.Anchors.Select(a => a.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetLenses_at_mapVersion_reads_that_version_and_reports_it_in_mapVersion_and_searched()
    {
        var created = await TabsThenOther();

        var one = (await _world.Engine.Lenses(Manager, V("v1"), depth: null, MapWorld.Human, MapWorld.None)).Value;
        var many = (await Set([Manager, Groups], read: V("1"))).Value;
        var current = await _world.Engine.Lenses(Manager, MapRead.Current, depth: null, MapWorld.Human, MapWorld.None);

        Assert.Equal(("v1", "v1", Manager), (one.MapVersion, one.Searched.MapVersion, one.Anchor.Key.Value));
        Assert.Equal(("v1", "v1"), (many.MapVersion, many.Searched.MapVersion));
        Assert.Equal(created, one.Coverage.Map!.Created);
        Assert.Equal(created, many.Coverage.Map!.Created);
        Assert.Equal(created, Assert.Single(one.Searched.Sources, s => s.Id.Value == "map").AsOf);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, current.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetLenses_at_a_version_that_is_not_held_is_120_for_one_anchor_and_for_a_set()
    {
        _ = await TabsThenOther();

        var one = await _world.Engine.Lenses(Manager, V("v99"), depth: null, MapWorld.Human, MapWorld.None);
        var many = await Set([Manager, Groups], read: V("v99"));

        Assert.Equal((ContextErrors.MapVersionNotRetained.Code, ContextErrors.MapVersionNotRetained.Code), (one.Error.Code, many.Error.Code));
        Assert.Contains("v2", many.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetLenses_set_lists_an_anchor_the_map_does_not_hold_and_21_anchors_are_006()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var missing = await Set([Manager, "nope:x"]);
        var nothing = await Set(["nope:x", "nope:y"]);
        var tooMany = await Set([.. Enumerable.Range(0, 21).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"nope:{i}"))]);

        Assert.Equal(["nope:x"], missing.Value.MissingAnchors);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, nothing.Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, tooMany.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetLenses_session_denied_anchor_is_missing()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var denying = new ContextScope(Settings: null, [new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "src/commands/**"] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

        var view = await Set([Manager, "code:src/commands/ids.ts"], denying);

        Assert.Equal(["code:src/commands/ids.ts"], view.Value.MissingAnchors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetLenses_session_set_is_scoped_to_its_repo_and_a_session_of_another_repo_finds_none()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var own = await Set([Manager, "nope:x"], MapWorld.Session("aw-split-panes"));
        var other = await Set([Manager, Groups], MapWorld.Session("other"));

        Assert.Equal(["nope:x"], own.Value.MissingAnchors);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, other.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Searched_says_the_map_is_ok_the_unconfigured_intent_source_so_and_the_code_index_unavailable()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var view = (await Set([Manager, Groups])).Value;

        var onyx = Assert.Single(view.Searched.Sources, s => s.Id.Value == "onyx");
        Assert.Equal((SearchedState.NotConfigured, "not-configured"), (onyx.State, onyx.Reason));
        var index = Assert.Single(view.Searched.Sources, s => s.Id.Value == "code-index");
        Assert.Equal((SearchedState.Unavailable, "index-not-built"), (index.State, index.Reason));
        Assert.Equal(SearchedState.Ok, Assert.Single(view.Searched.Sources, s => s.Id.Value == "map").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_coverage_counts_the_repos_files_under_the_anchors_paths()
    {
        _ = await _world.ImportFixture();

        var view = (await _world.Engine.Lenses("feature:layout.split-pane", MapRead.Current, depth: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.NotEmpty(view.Coverage.Map!.Paths);
        Assert.True(view.Coverage.Map.Files > 0);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_built_at_a_commit_keeps_mapVersion_as_v_N_and_carries_the_commit_in_mapCommit()
    {
        _ = await _world.Map.BuildMap(progress: null, MapWorld.Human, MapWorld.None);
        var promoted = await _world.Map.Promote(MapWorld.Human, MapWorld.None);
        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        var anchor = (await _world.Store.Snapshot(draft: false, MapWorld.None)).Value!.Graph.Nodes[0].Key.Value;

        var view = (await Set([anchor])).Value;
        var one = (await _world.Engine.Lenses(anchor, MapRead.Current, depth: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(("v1", MapWorld.Head[..7]), (view.MapVersion, view.MapCommit));
        Assert.Equal(("v1", MapWorld.Head[..7]), (one.MapVersion, one.MapCommit));
        Assert.Equal("v1", view.Searched.MapVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fenced_caller_reads_the_new_labels_cleaned_like_the_old_ones()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var view = (await Set([Manager], MapWorld.HumanVia(ContextClient.Mcp))).Value;

        Assert.Contains(view.Bands.Intent.Decisions, d => d.Trust == Trust.Untrusted);
        Assert.All(view.Bands.Intent.Decisions.Where(d => d.Trust == Trust.Untrusted), d => Assert.StartsWith("<untrusted", d.Label, StringComparison.Ordinal));
        var suggested = Assert.Single(view.Bands.Code.Files, f => !f.Confirmed);
        Assert.Equal(suggested.Path, suggested.Label);
    }
}
