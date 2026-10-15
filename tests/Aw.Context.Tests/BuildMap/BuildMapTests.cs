using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Features.BuildMap;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

public sealed class BuildMapTests : IDisposable
{
    private static readonly NodeKey _split = NodeKey.From("surface:command/split-vertical");
    private static readonly NodeKey _close = NodeKey.From("surface:command/close-pane");
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-14T20:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private readonly MapWorld _world = new();

    private static ContextScope Scope => MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    private static ContextScope Labeling(int maxTokens = 5_000) =>
        MapWorld.HumanWith("""{"repos":["aw-split-panes"],"labeling":{"enabled":true,"maxTokens":""" + maxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}}");

    private static FileCluster Cluster(params string[] files) => new(ClusterId.From("c1"), files, 0.7);

    public void Dispose() => _world.Dispose();

    private async Task<IReadOnlyList<MapVersion>> Versions() => (await _world.Store.Versions(MapWorld.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_writes_a_draft_with_each_proposed_node_its_provenance_confidence_and_files()
    {
        _ = await _world.ImportFixture();

        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        Assert.Equal((MapVersionStatus.Draft, FixtureMap.Repo, MapWorld.Head), (report.Draft.Status, report.Draft.Repo.Value, report.Draft.Commit));
        var split = Assert.Single(report.Proposed, p => p.Key.Value == _split.Value);
        Assert.Equal((Provenance.Deterministic, 1.0), (split.Provenance, split.Confidence));
        Assert.Equal(["code:src/commands/ids.ts", "code:src/commands/registry.ts"], split.Files.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Contains(report.Proposed, p => p.Key.Value == "surface:setting/paneResizeStep");
        Assert.Contains(report.Proposed, p => p.Key.Value == "surface:keybinding/ctrl+shift+d");
        Assert.DoesNotContain(report.Proposed, p => p.Key.Kind == NodeKind.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_leaves_the_current_map_alone()
    {
        var imported = await _world.ImportFixture();

        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        var versions = await Versions();
        Assert.Equal(imported.Version.Number, Assert.Single(versions, v => v.Status == MapVersionStatus.Current).Number);
        Assert.Equal(report.Draft.Number, Assert.Single(versions, v => v.Status == MapVersionStatus.Draft).Number);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_graphify_the_build_goes_on_with_deterministic_anchors_and_says_degraded_clusters()
    {
        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        Assert.Equal(["clusters"], report.Degraded);
        Assert.All(report.Proposed, p => Assert.Equal(Provenance.Deterministic, p.Provenance));
        Assert.NotEmpty(report.Proposed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Clusters_become_candidate_features_with_model_provenance()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);

        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        var feature = Assert.Single(report.Proposed, p => p.Key.Kind == NodeKind.Feature);
        Assert.Equal((Provenance.Model, 0.7), (feature.Provenance, feature.Confidence));
        Assert.Equal(["code:src/commands/ids.ts", "code:src/commands/registry.ts"], feature.Files.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Empty(report.Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Labeling_is_off_unless_the_profile_turns_it_on_and_the_labeler_is_never_asked()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);

        _ = await _world.Map.BuildMap(progress: null, Scope, MapWorld.None);

        Assert.Empty(_world.Labeler.Shown);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_labeling_on_the_models_names_label_the_clusters()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);

        _ = await _world.Map.BuildMap(progress: null, Labeling(), MapWorld.None);

        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        Assert.Equal("Labeled c1", Assert.Single(draft.Graph.Nodes, n => n.Key.Kind == NodeKind.Feature).Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_labeling_pass_over_its_cap_is_SND_CONTEXT_042_and_no_draft_appears()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);
        _world.Labeler.Failure = ContextErrors.LabelingUnavailable.With("over the cap (fix: raise 'context.labeling.maxTokens')");

        var report = await _world.Map.BuildMap(progress: null, Labeling(), MapWorld.None);

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, report.Error.Code);
        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_model_that_cant_answer_leaves_the_file_derived_names_and_says_degraded_labels()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);
        _world.Labeler.Failure = ContextErrors.SourceUnavailable.With("the model is down");

        var report = (await _world.Map.BuildMap(progress: null, Labeling(), MapWorld.None)).Value;

        Assert.Equal(["labels"], report.Degraded);
        _ = Assert.Single(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Labeling_turned_on_with_no_labeler_set_up_is_SND_CONTEXT_042()
    {
        var labels = await new DisabledLabeler().Label([Cluster("a.ts")], MapWorld.None);

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, labels.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_heavy_job_lock_is_SND_CONTEXT_040_naming_the_holder_and_nothing_is_written()
    {
        _world.HeavyLock.BusyHolder = "pid 4242 (bash-tests)";
        var progress = new SyncProgress();

        var report = await _world.Map.BuildMap(progress, Scope, MapWorld.None);

        Assert.Equal(ContextErrors.HeavyLockBusy.Code, report.Error.Code);
        Assert.Contains("pid 4242", report.Error.Message, StringComparison.Ordinal);
        Assert.Contains(progress.Lines, l => l.StartsWith("waiting for heavy-job lock", StringComparison.Ordinal));
        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_slot_is_released_when_the_build_ends_and_when_it_fails()
    {
        _ = await _world.Map.BuildMap(progress: null, Scope, MapWorld.None);
        Assert.Equal((1, 0), (_world.HeavyLock.Taken, _world.HeavyLock.Held));

        var failed = await _world.Map.BuildMap(progress: null, MapWorld.Human, MapWorld.None);   // the fake repo reader knows only one of the two repos

        Assert.True(failed.IsError);
        Assert.Equal((2, 0), (_world.HeavyLock.Taken, _world.HeavyLock.Held));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_git_cant_read_is_SND_CONTEXT_048_and_writes_no_draft()
    {
        var report = await _world.Map.BuildMap(progress: null, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.ExtractionUnreadable.Code, report.Error.Code);
        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Progress_says_how_many_files_were_extracted()
    {
        var progress = new SyncProgress();

        _ = await _world.Map.BuildMap(progress, Scope, MapWorld.None);

        Assert.Equal("extracting 5/5 files", progress.Lines.Last(l => l.StartsWith("extracting ", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_build_leaves_no_draft()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _world.Map.BuildMap(progress: null, Scope, cancelled.Token));

        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_cant_build()
    {
        var report = await _world.Map.BuildMap(progress: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.Equal(ContextErrors.SessionsDisabled.Code, report.Error.Code);
        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_build_keeps_every_decision_and_re_proposes_no_rejected_node()
    {
        _ = await _world.Map.BuildMap(progress: null, Scope, MapWorld.None);
        var first = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var rejected = first.Graph.Resolve(_close)!;
        var proposed = new MapOverride(OverrideAction.Propose, NodeKey.From("feature:tabs.restore"), "service:engine", _at, "Restore tabs",
            Proposal: new ProposalPayload("run-1", ContextRepoName.From(FixtureMap.Repo), NodeKind.Feature));
        _ = await _world.Store.AppendOverrides(
        [
            new MapOverride(OverrideAction.Accept, _split, "avery", _at),
            new MapOverride(OverrideAction.Reject, _close, "avery", _at, Rejection: RejectionIdentity.For(rejected)),
            proposed,
        ], MapWorld.None);

        var second = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        var snapshot = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var applied = OverrideApplier.Apply(snapshot.Graph, snapshot.Overrides).Graph;
        Assert.NotEqual(first.Version.Number, second.Draft.Number);
        Assert.Equal(Provenance.Human, applied.Resolve(_split)!.Provenance);
        Assert.Null(applied.Resolve(_close));
        Assert.NotNull(applied.Resolve(NodeKey.From("feature:tabs.restore")));
        _ = Assert.Single(await Versions(), v => v.Status == MapVersionStatus.Draft);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Denied_paths_never_reach_the_recognizers_the_clusters_or_the_labeler()
    {
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal) { ["private/settings.json"] = "{\"apiToken\": \"hunter2\"}" };
        var reader = new LeakyRepoReader(MapWorld.Head, files);
        using var world = new MapWorld(reader);
        var repo = new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };
        world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "private/settings.json", "config/secrets/settings.json")], Available: true);

        var report = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith("""{"repos":["aw-split-panes"],"labeling":{"enabled":true}}""", [repo]), MapWorld.None)).Value;

        Assert.DoesNotContain(reader.Reads, r => string.Equals(r, "private/settings.json", StringComparison.Ordinal) || string.Equals(r, ".env", StringComparison.Ordinal) || string.Equals(r, "config/secrets/settings.json", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Proposed, p => p.Key.Value == "surface:setting/apiToken");
        Assert.DoesNotContain(report.Proposed.SelectMany(p => p.Files), f => f.Value.Contains("private/", StringComparison.Ordinal) || f.Value.Contains("secrets/", StringComparison.Ordinal));
        Assert.Equal(["src/commands/ids.ts"], world.Labeler.Shown.SelectMany(c => c.Files), StringComparer.Ordinal);
        var draft = (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        Assert.DoesNotContain(draft.Graph.Nodes, n => n.Key.Value.Contains("private/", StringComparison.Ordinal) || n.Key.Value.Contains("secrets/", StringComparison.Ordinal));
    }
}
