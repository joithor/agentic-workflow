using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.MapPublish;

/// <summary>The map side of publishing together with the code index (retrieval spec §8.7a): the build's heads, the hold, the expected draft and the scoped status.</summary>
public sealed class MapPublishTests : IDisposable
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private static readonly ContextScope _service = Drafts.Scope with { Caller = new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service) };

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<MapBuildReport> BuildAndSettle()
    {
        var report = await Drafts.Build(_world);
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), Drafts.Scope, MapWorld.None)).IsSuccess);
        return report;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_reports_each_mapped_repos_commit_and_whether_the_default_branch_holds_it_and_stores_them_with_the_draft()
    {
        _ = _world.DefaultBranch.Reach(_repo, MapWorld.Head);

        var report = await Drafts.Build(_world);

        Assert.Equal([new MapHead(_repo, MapWorld.Head, OnDefault: true)], report.Heads);
        Assert.Equal(report.Heads, (await _world.Store.Heads(report.Draft.Number, MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_off_the_default_branch_says_so()
    {
        var report = await Drafts.Build(_world);   // the fake default branch knows no commit

        Assert.False(Assert.Single(report.Heads).OnDefault);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_service_holds_the_draft_reads_it_back_and_releases_it()
    {
        var report = await BuildAndSettle();
        var now = _world.Clock.GetUtcNow();

        var held = await _world.Map.Hold(new MapHold(report.Draft.Number, now, []), _service, MapWorld.None);
        var state = (await _world.Map.Held(_service, MapWorld.None)).Value;

        Assert.Equal((report.Draft.Number, now, report.Heads), (held.Value.Version, held.Value.Since, held.Value.Heads));
        Assert.Equal((held.Value.Version, held.Value.Since, report.Heads, report.Draft.Number, null), (state.Hold!.Version, state.Hold.Since, state.Hold.Heads, state.LatestDraft, state.Current));
        Assert.True((await _world.Map.Release(report.Draft.Number, _service, MapWorld.None)).IsSuccess);
        Assert.Null((await _world.Map.Held(_service, MapWorld.None)).Value.Hold);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Held_hold_and_release_refuse_a_session()
    {
        var report = await BuildAndSettle();
        var session = MapWorld.Session(_repo.Value);

        Assert.Equal(ContextErrors.SessionsDisabled.Code, (await _world.Map.Held(session, MapWorld.None)).Error.Code);
        Assert.Equal(ContextErrors.SessionsDisabled.Code, (await _world.Map.Hold(new MapHold(report.Draft.Number, _world.Clock.GetUtcNow(), []), session, MapWorld.None)).Error.Code);
        Assert.Equal(ContextErrors.SessionsDisabled.Code, (await _world.Map.Release(report.Draft.Number, session, MapWorld.None)).Error.Code);
        Assert.Null((await _world.Map.Held(_service, MapWorld.None)).Value.Hold);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Any_promote_through_the_engine_clears_the_hold_and_the_wait_and_keeps_the_heads()
    {
        var report = await BuildAndSettle();
        _ = await _world.Map.Hold(new MapHold(report.Draft.Number, _world.Clock.GetUtcNow(), []), _service, MapWorld.None);

        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None);

        var state = (await _world.Map.Held(_service, MapWorld.None)).Value;
        Assert.True(promoted.IsSuccess);
        Assert.Equal((null, null, promoted.Value.Version.Number), (state.Hold, state.LatestDraft, state.Current));
        Assert.Equal(report.Heads, state.CurrentHeads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_that_names_the_draft_it_decided_about_promotes_it()
    {
        var report = await BuildAndSettle();

        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None, expectedVersion: report.Draft.Number);

        Assert.Equal(report.Draft.Number, promoted.Value.Version.Number);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_that_names_another_draft_is_190_and_promotes_nothing()
    {
        var first = await BuildAndSettle();
        var second = await BuildAndSettle();   // a rebuild replaced the draft the caller saw

        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None, expectedVersion: first.Draft.Number);

        Assert.Equal(ContextErrors.DraftChanged.Code, promoted.Error.Code);
        Assert.Equal([(second.Draft.Number, MapVersionStatus.Draft)], (await _world.Store.Versions(MapWorld.None)).Value.Select(v => (v.Number, v.Status)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_that_names_a_draft_when_there_is_none_is_190_but_without_a_name_it_is_046()
    {
        var report = await BuildAndSettle();
        Assert.True((await _world.Map.Promote(Drafts.Scope, MapWorld.None)).IsSuccess);   // promoted since the caller decided about it

        Assert.Equal(ContextErrors.DraftChanged.Code, (await _world.Map.Promote(Drafts.Scope, MapWorld.None, expectedVersion: report.Draft.Number)).Error.Code);
        Assert.Equal(ContextErrors.PromoteRefused.Code, (await _world.Map.Promote(Drafts.Scope, MapWorld.None)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_whose_draft_is_replaced_between_the_read_and_the_write_is_046_and_never_promotes_the_new_draft()
    {
        var report = await BuildAndSettle();
        var rebuilt = false;
        _world.Store.AfterSnapshot = draft =>
        {
            if (draft && !rebuilt)
            {
                rebuilt = true;
                _ = _world.Store.SaveDraft(SampleMapOf(report), _world.Clock.GetUtcNow(), MapWorld.None).AsTask().GetAwaiter().GetResult();
            }
        };

        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None, expectedVersion: report.Draft.Number);

        Assert.True(promoted.IsError);
        Assert.DoesNotContain((await _world.Store.Versions(MapWorld.None)).Value, v => v.Status == MapVersionStatus.Current);
    }

    private static MapImport SampleMapOf(MapBuildReport report) => new(report.Draft.Repo, report.Draft.Commit, Context.Core.Graph.GraphSnapshot.Empty, []);

    [Fact, Trait("Lane", "unit")]
    public async Task A_cleanup_that_names_another_draft_is_190_and_writes_no_rows()
    {
        var first = await Drafts.Build(_world);
        var second = await Drafts.Build(_world);
        var rows = await Drafts.Rows(_world);

        var cleaned = await _world.Map.CleanDraft(Drafts.Scope, MapWorld.None, expectedVersion: first.Draft.Number);

        Assert.Equal(ContextErrors.DraftChanged.Code, cleaned.Error.Code);
        Assert.Equal(rows, await Drafts.Rows(_world));
        Assert.Equal(second.Draft.Number, (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Version.Number);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cleanup_that_names_the_latest_draft_settles_it()
    {
        var report = await Drafts.Build(_world);

        var cleaned = await _world.Map.CleanDraft(Drafts.Scope, MapWorld.None, expectedVersion: report.Draft.Number);

        Assert.True(cleaned.IsSuccess);
        Assert.Equal(0, (await Drafts.Queue(_world)).Undecided);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cleanup_whose_draft_is_replaced_during_the_pass_appends_nothing_for_the_old_draft()
    {
        var report = await Drafts.Build(_world);
        var rows = await Drafts.Rows(_world);
        var reads = 0;
        _world.Store.AfterSnapshot = draft =>
        {
            if (draft && ++reads == 1)   // after the pass has read the draft and before it appends
            {
                _ = _world.Store.SaveDraft(SampleMapOf(report), _world.Clock.GetUtcNow(), MapWorld.None).AsTask().GetAwaiter().GetResult();
            }
        };

        var cleaned = await _world.Map.CleanDraft(Drafts.Scope, MapWorld.None, expectedVersion: report.Draft.Number);

        Assert.Equal(ContextErrors.DraftChanged.Code, cleaned.Error.Code);
        Assert.Equal(rows, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_status_shows_a_human_every_repo_and_a_session_only_its_own()
    {
        var report = await BuildAndSettle();
        _ = await _world.Map.Hold(new MapHold(report.Draft.Number, _world.Clock.GetUtcNow(), []), _service, MapWorld.None);
        var two = Human2();

        var human = (await _world.Map.PublishStatus(two, MapWorld.None)).Value;
        var session = (await _world.Map.PublishStatus(MapWorld.Session(_repo.Value), MapWorld.None)).Value;

        Assert.Equal([_repo.Value, "other"], human.Repos.Select(r => r.Value), StringComparer.Ordinal);
        Assert.Equal(report.Draft.Number, human.Hold!.Version);
        Assert.Equal([_repo.Value], session.Repos.Select(r => r.Value), StringComparer.Ordinal);
        Assert.All(session.Hold!.Heads, h => Assert.Equal(_repo, h.Repo));
        Assert.All(session.CurrentHeads, h => Assert.Equal(_repo, h.Repo));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sessions_status_names_no_other_repos_commit()
    {
        var report = await BuildAndSettle();
        // a second repo's head on the draft: the build above mapped one repo, so add the other by a hold-time head the store keeps
        _ = await _world.Store.SaveDraft(new MapImport(_repo, MapWorld.Head, Context.Core.Graph.GraphSnapshot.Empty, [],
            [new MapHead(_repo, "1111111", OnDefault: true), new MapHead(ContextRepoName.From("other"), "2222222", OnDefault: true)]), _world.Clock.GetUtcNow(), MapWorld.None);
        var latest = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Version.Number;
        _ = await _world.Store.Hold(new MapHold(latest, _world.Clock.GetUtcNow(), []), MapWorld.None);
        Assert.NotEqual(report.Draft.Number, latest);

        var session = (await _world.Map.PublishStatus(MapWorld.Session(_repo.Value), MapWorld.None)).Value;

        Assert.Equal([new MapHead(_repo, "1111111", OnDefault: true)], session.Hold!.Heads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_is_refused_while_sessions_are_off_and_for_a_repo_the_profile_does_not_map()
    {
        _ = await BuildAndSettle();

        Assert.Equal(ContextErrors.SessionsDisabled.Code, (await _world.Map.PublishStatus(MapWorld.Session(_repo.Value, enabled: false), MapWorld.None)).Error.Code);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, (await _world.Map.PublishStatus(MapWorld.Session("not-mapped"), MapWorld.None)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_map_at_all_the_status_is_empty_not_an_error()
    {
        var status = (await _world.Map.PublishStatus(Drafts.Scope, MapWorld.None)).Value;

        Assert.Equal((null, null, null), (status.Hold, status.LatestDraft, status.Current));
        Assert.Empty(status.CurrentHeads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_file_with_a_commit_that_is_not_lowercase_hex_is_refused_on_import()
    {
        var yaml = MapWorld.Yaml("schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "commit: NOT-A-COMMIT", "nodes: []");
        const string path = "/maps/bad-commit.yaml";
        _world.Reader.Put(path, Maps.Parse(yaml), yaml);

        var preview = await _world.Map.PreviewMap(path, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.MapFileInvalid.Code, preview.Error.Code);
        Assert.Contains("commit", preview.Error.Message, StringComparison.Ordinal);
    }

    private static ContextScope Human2() => MapWorld.Human;
}
