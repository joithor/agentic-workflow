using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Review;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.Map;

/// <summary>
/// One denied-file scenario through every reader (review #220 run 3: the structural fix). There is one function from a stored
/// snapshot to what a caller may see (MapAccess.View), and every reader goes through it, so none can disagree with another.
/// </summary>
public sealed class OneVisibleViewTests : IDisposable
{
    private static readonly string[] _hidden =
    [
        "code:private/a.ts", "code:private/c.ts", "feature:drafted", "feature:accepted", "surface:setting/secret", "surface:setting/proposed",
    ];

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    public static TheoryData<string> HiddenKeys => [.. _hidden];

    public static TheoryData<string> AnchorReaders => ["show", "lenses", "lenses-draft", "neighbors", "status", "path", "query"];

    private static ContextScope Denying() =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    // The map was built before the glob: a model feature whose only file is private (one a person accepted), a deterministic surface only a private
    // file produced, a visible deterministic surface, and a propose row of a scoping run whose evidence is a private file.
    private async Task Scenario()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"feature:drafted\", label: \"Drafted\", provenance: model}",
            "  - {key: \"feature:accepted\", label: \"Accepted\", provenance: model}",
            "  - {key: \"code:private/a.ts\", label: \"a.ts\"}",
            "  - {key: \"code:private/c.ts\", label: \"c.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "  - {key: \"surface:setting/secret\", label: \"secret\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}",
            "  - {key: \"surface:setting/open\", label: \"open\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/b.ts\"}]}",
            "edges:",
            "  - {from: \"code:private/a.ts\", to: \"feature:drafted\", type: implements}",
            "  - {from: \"code:private/c.ts\", to: \"feature:accepted\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"surface:setting/secret\", to: \"feature:kept\", type: part-of, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}",
            "  - {from: \"surface:setting/open\", to: \"feature:kept\", type: part-of, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/b.ts\"}]}"));
        var proposed = await _world.Map.Propose(
            new ScopeProposal("run-1", ContextRepoName.From(FixtureMap.Repo),
            [new ProposedSurface("Proposed", NodeKey.From("surface:setting/proposed"), NodeKind.Surface, Parent: null, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/private/p.ts"))], Quote: null)]),
            MapWorld.Human, MapWorld.None);
        Assert.True(proposed.IsSuccess, proposed.IsError ? proposed.Error.Message : null);
        var accepted = await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Accept, NodeKey.From("feature:accepted")), Drafts.Scope, MapWorld.None);
        Assert.True(accepted.IsSuccess, accepted.IsError ? accepted.Error.Message : null);
    }

    private async Task<string> Answer(string reader, string key)
    {
        var scope = Denying();
        return reader switch
        {
            "show" => Code(await _world.Engine.ShowNode(key, scope, MapWorld.None)),
            "lenses" => Code(await _world.Engine.Lenses(key, read: MapRead.Current, depth: null, scope, MapWorld.None)),
            "lenses-draft" => Code(await _world.Engine.Lenses(key, read: MapRead.NewestDraft, depth: null, scope, MapWorld.None)),
            "neighbors" => Code(await _world.Engine.Neighbors(key, depth: 1, scope, MapWorld.None)),
            "status" => Code(await _world.Map.MapStatus(key, scope, MapWorld.None)),
            "path" => Code(await _world.Engine.Path(key, "feature:kept", scope, MapWorld.None)),
            "query" => Code(await _world.Engine.Query(new ContextQuery(Text: null, Symbol: key), scope, MapWorld.None)),
            _ => throw new ArgumentOutOfRangeException(nameof(reader)),
        };
    }

    private static string Code<T>(Kernel.Result<T> result) => result.IsError ? result.Error.Code : "answered";

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(AnchorReaders), DisableDiscoveryEnumeration = true)]
    public async Task Every_anchored_reader_answers_003_for_every_hidden_key(string reader)
    {
        await Scenario();

        var answers = new List<string>();
        foreach (var key in _hidden)
        {
            answers.Add($"{key}: {await Answer(reader, key)}");
        }

        Assert.All(answers, a => Assert.EndsWith(ContextErrors.UnknownAnchor.Code, a, StringComparison.Ordinal));
    }

    private async Task<List<string>> ExportNames(ContextScope scope)
    {
        var exported = await _world.Map.ExportMap(path: null, scope, MapWorld.None);
        var document = _world.Exports.Documents[exported.Value.Path];
        return
        [
            .. document.Nodes.Select(n => n.Key.Value),
            .. (document.Edges ?? []).SelectMany(e => new[] { e.From.Value, e.To.Value }),
            .. document.Nodes.SelectMany(n => n.EvidenceOrEmpty).Select(r => r.Key.Value),
            .. (document.Overrides ?? []).SelectMany(o => new[] { o.Target.Value, o.Other?.Value ?? string.Empty, o.Link?.Value ?? string.Empty }.Concat((o.Proposal?.Evidence ?? []).Select(r => r.Key.Value))),
            .. (document.Orphaned ?? []).Select(k => k.Value),
        ];
    }

    private async Task<Dictionary<string, List<string>>> Listings(ContextScope scope)
    {
        var queue = (await _world.Map.ReviewQueue(limit: null, scope, MapWorld.None)).Value;
        var history = (await _world.Map.ReviewHistory(limit: null, scope, MapWorld.None)).Value;
        var fileMap = (await _world.Engine.FileMap(new FileMapQuery(ContextRepoName.From(FixtureMap.Repo), Path: ".", UnmappedOnly: false, Draft: false), scope, MapWorld.None)).Value;
        var proposals = (await _world.Map.Proposals("run-1", scope, MapWorld.None)).Value;
        return new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["queue"] = [.. queue.Entries.SelectMany(e => e.Files.Select(f => f.Value).Append(e.Node.Value).Append(e.Label))],
            ["export"] = await ExportNames(scope),
            ["history"] = [.. history.Entries.SelectMany(e => new[] { e.Target.Value, e.Label ?? string.Empty })],
            ["filemap"] = [.. fileMap.Entries.SelectMany(e => e.Features.Select(f => f.Key.Value).Append(e.Path))],
            ["proposals"] = [.. proposals.Surfaces.Where(s => s.Node is not null).Select(s => s.Node!.Value.Value)],   // why: filtered to the ones with a node
        };
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_listing_reader_names_nothing_hidden_and_something_visible()
    {
        await Scenario();

        var seen = await Listings(Denying());

        foreach (var names in seen.Values)
        {
            Assert.DoesNotContain(names, n => n.Contains("private/", StringComparison.Ordinal) || _hidden.Contains(n, StringComparer.Ordinal));
        }

        Assert.Contains("surface:setting/open", seen["queue"], StringComparer.Ordinal);
        Assert.Contains("feature:kept", seen["export"], StringComparer.Ordinal);
        Assert.Contains("src/", seen["filemap"], StringComparer.Ordinal);   // the repo root lists its folders; private/ is not one
        var proposals = (await _world.Map.Proposals("run-1", Denying(), MapWorld.None)).Value;
        var surface = Assert.Single(proposals.Surfaces);
        Assert.Equal(("Proposed", GraphStatusKind.Unmapped, null), (surface.Surface, surface.Status, surface.Node));   // the one surface the run proposed has only denied evidence: unmapped, no node

        await Decide(Drafts.Scope, On(ReviewVerb.Accept, "surface:setting/open"));
        var decided = await Listings(Denying());
        Assert.Contains("surface:setting/open", decided["history"], StringComparer.Ordinal);   // a visible decision is listed
        Assert.DoesNotContain(decided["history"], n => n.Contains("private/", StringComparison.Ordinal) || _hidden.Contains(n, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_applied_review_file_exported_before_the_glob_decides_only_what_the_denied_queue_shows()
    {
        await Scenario();
        var all = (await _world.Map.ReviewQueue(limit: null, Drafts.Scope, MapWorld.None)).Value;
        var shown = (await _world.Map.ReviewQueue(limit: null, Denying(), MapWorld.None)).Value;
        var file = new ReviewFile(all.Draft, [.. all.Entries.Where(e => e.Group != ReviewGroup.Orphaned).Select(e =>
            new ReviewFileEntry(e.Label, e.Group, e.Confidence, e.Link is null ? e.Node : null, e.Link, Decision: ReviewVerb.Accept))]);
        var path = (await _world.ReviewFiles.Write(Path.Combine(FakeReviewFileStore.Folder, "old.json"), "old.json", file, MapWorld.None)).Value;

        var applied = (await _world.Map.ApplyReviewFile(path, Denying(), MapWorld.None)).Value;

        var targets = (await _world.Store.Overrides(MapWorld.None)).Value.Where(o => applied.Written.Contains(o.ToId())).Select(o => o.Target.Value).ToList();
        Assert.NotEmpty(targets);
        Assert.DoesNotContain(targets, t => _hidden.Contains(t, StringComparer.Ordinal));
        Assert.Equal(file.Entries.Count - shown.Entries.Count, applied.Skipped.Count);   // every hidden entry is skipped
        Assert.Equal((await _world.Map.ReviewQueue(limit: null, Denying(), MapWorld.None)).Value.Undecided, applied.Undecided);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_review_answers_and_the_queue_count_the_same_undecided_items_and_the_bulk_accept_writes_only_what_is_shown()
    {
        await Scenario();
        var scope = Denying();
        var plain = (await _world.Map.ReviewQueue(limit: null, MapWorld.Human, MapWorld.None)).Value;
        var queue = (await _world.Map.ReviewQueue(limit: null, scope, MapWorld.None)).Value;

        var refused = await _world.Map.Promote(scope, MapWorld.None);
        var bulk = (await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), scope, MapWorld.None)).Value;
        var after = (await _world.Map.ReviewQueue(limit: null, scope, MapWorld.None)).Value;

        Assert.True(plain.Undecided > queue.Undecided);
        Assert.Contains(queue.Undecided.ToString(CultureInfo.InvariantCulture) + " undecided", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(queue.Counts.Deterministic, bulk.Written.Count);
        Assert.Equal((0, 0), (bulk.Undecided, after.Undecided));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_the_preview_would_allow_succeeds_and_the_denied_paths_leave_the_stored_map()
    {
        await Scenario();
        var scope = Denying();
        _ = await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), scope, MapWorld.None);

        var promoted = await _world.Map.Promote(scope, MapWorld.None);

        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        var current = (await _world.Store.Snapshot(draft: false, MapWorld.None)).Value!;
        var stored = current.Graph.Nodes.Select(n => n.Key.Value).Concat(current.Graph.Edges.SelectMany(e => new[] { e.From.Value, e.To.Value }))
            .Concat(current.Graph.Nodes.SelectMany(n => n.EvidenceOrEmpty).Select(r => r.Key.Value)).ToList();
        Assert.DoesNotContain(stored, n => n.Contains("private/", StringComparison.Ordinal));
        Assert.DoesNotContain(stored, n => _hidden.Contains(n, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_lenses_status_and_export_agree_on_the_nodes_and_overrides_a_denied_file_leaves()
    {
        await Scenario();
        var scope = Denying();

        var status = (await _world.Map.MapStatus("feature:kept", scope, MapWorld.None)).Value;
        var lenses = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, scope, MapWorld.None)).Value;
        var exported = (await _world.Map.ExportMap(path: null, scope, MapWorld.None)).Value;
        var document = _world.Exports.Documents[exported.Path];

        Assert.Equivalent(lenses.Coverage with { Map = null }, status.Coverage);
        Assert.Equal(status.Counts, exported.Counts);
        Assert.Equal(status.Counts.Nodes, document.Nodes.Count);
        Assert.Equal(0, status.Orphaned);
        Assert.DoesNotContain(document.Overrides ?? [], o => o.Target.Value == "feature:accepted");   // the human's accept goes with the feature it was about
    }

    // ---- one order for the cut and the overrides (review #220 run 4) ----

    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);

    private static ReviewDecision On(ReviewVerb verb, string key, string? other = null) =>
        new(verb, NodeKey.From(key), Other: other is null ? null : NodeKey.From(other));

    // A map imported as the current version, and a draft opened as a copy of it.
    private async Task DraftOf(params string[] lines)
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(["schemaVersion: 1", "kind: authored", "repo: aw-split-panes", .. lines]));
        var opened = await _world.Store.OpenDraft(_world.Clock.GetUtcNow(), MapWorld.None);
        Assert.True(opened.IsSuccess, opened.IsError ? opened.Error.Message : null);
    }

    private async Task Decide(ContextScope scope, ReviewDecision decision)
    {
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        var done = await _world.Map.ApplyReview(decision, scope, MapWorld.None);
        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
    }

    private async Task<ProposeResult> Run(string run, ContextScope scope, params ProposedSurface[] surfaces)
    {
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        var proposed = await _world.Map.Propose(new ScopeProposal(run, _repo, surfaces), scope, MapWorld.None);
        Assert.True(proposed.IsSuccess, proposed.IsError ? proposed.Error.Message : null);
        return proposed.Value;
    }

    private static ProposedSurface Surface(string name, string key, params string[] evidence) =>
        new(name, NodeKey.From(key), NodeKind.Surface, Parent: null, [.. evidence.Select(e => new EvidenceRef(RecordKey.From("repo:aw-split-panes/" + e)))], Quote: null);

    private async Task<List<string>> StoredNames()
    {
        var current = (await _world.Store.Snapshot(draft: false, MapWorld.None)).Value!;
        return
        [
            .. current.Graph.Nodes.Select(n => n.Key.Value), .. current.Graph.Edges.SelectMany(e => new[] { e.From.Value, e.To.Value }),
            .. current.Graph.Nodes.SelectMany(n => n.EvidenceOrEmpty).Select(r => r.Key.Value), .. current.Graph.Decisions.Select(d => d.Record.Value),
            .. current.Graph.Aliases.SelectMany(a => new[] { a.Alias.Value, a.Target.Value }),
        ];
    }

    // The exports, the status counts and the promoted version name nothing under private/ and none of the keys a person's decision took out of the applied graph.
    private async Task AssertNeitherSeenNorPromoted(int visibleNodes, params string[] hidden) => await AssertNeitherSeenNorPromoted(visibleNodes, hidden, notPromoted: hidden);

    // 'hidden' keys are named by no export (the overrides about a proposal that is only rejected stay: a person decided them); 'notPromoted' keys are not in the promoted version.
    private async Task AssertNeitherSeenNorPromoted(int visibleNodes, string[] hidden, string[] notPromoted)
    {
        var scope = Denying();
        var names = await ExportNames(scope);
        var status = (await _world.Map.MapStatus(anchor: null, scope, MapWorld.None)).Value;
        Assert.DoesNotContain(names, n => n.Contains("private/", StringComparison.Ordinal) || hidden.Contains(n, StringComparer.Ordinal));
        Assert.Equal(visibleNodes, status.Counts.Nodes);

        var promoted = await _world.Map.Promote(scope, MapWorld.None);

        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        var stored = await StoredNames();
        Assert.DoesNotContain(stored, n => n.Contains("private/", StringComparison.Ordinal) || notPromoted.Contains(n, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_node_a_person_rejected_is_in_no_export_status_count_or_promoted_version()
    {
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "  - {key: \"surface:setting/secret\", label: \"secret\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}",
            "edges:",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"surface:setting/secret\", to: \"feature:kept\", type: part-of, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}");
        await Decide(Drafts.Scope, On(ReviewVerb.Reject, "surface:setting/secret"));

        await AssertNeitherSeenNorPromoted(visibleNodes: 2, "surface:setting/secret");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_code_node_a_person_deleted_is_in_no_export_status_count_or_promoted_version()
    {
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:private/a.ts\", label: \"a.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:private/a.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}");
        await Decide(Drafts.Scope, On(ReviewVerb.Delete, "code:private/a.ts"));

        await AssertNeitherSeenNorPromoted(visibleNodes: 2, "code:private/a.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_code_node_a_person_merged_away_takes_its_links_with_it()
    {
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"feature:other\", label: \"Other\"}",
            "  - {key: \"code:private/c.ts\", label: \"c.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:private/c.ts\", to: \"feature:other\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}");
        await Decide(Drafts.Scope, On(ReviewVerb.Merge, "code:private/c.ts", other: "code:src/b.ts"));

        var other = (await _world.Engine.Lenses("feature:other", read: MapRead.Current, depth: null, Denying(), MapWorld.None)).Value;

        Assert.DoesNotContain(other.Bands.Code.Files, f => f.Path.Contains("b.ts", StringComparison.Ordinal));   // c's link is not b's
        await AssertNeitherSeenNorPromoted(visibleNodes: 3, "code:private/c.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_record_a_decision_quotes_leaves_the_lenses_the_query_the_export_and_the_stored_map()
    {
        const string record = "repo:aw-split-panes/private/notes.md";
        const string quote = "never ship the signing key";
        var decision = DecisionRow.KeyFor(RecordKey.From(record), quote).Value;
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "edges:",
            "  - {from: \"" + decision + "\", to: \"feature:kept\", type: decides}",
            "decisions:",
            "  - {record: \"" + record + "\", quote: \"" + quote + "\", wording: \"Keys stay out of the repo.\", at: 2026-07-15T10:00:00Z}");
        var before = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, Drafts.Scope, MapWorld.None)).Value;

        var lenses = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, Denying(), MapWorld.None)).Value;
        var query = (await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:kept"), Denying(), MapWorld.None)).Value;

        _ = Assert.Single(before.Bands.Intent.Decisions);
        Assert.Empty(lenses.Bands.Intent.Decisions);
        Assert.DoesNotContain(quote, string.Join('\n', query.Sections.SelectMany(s => s.Items).SelectMany(i => new[] { i.Title, i.Text })), StringComparison.Ordinal);
        var exported = (await _world.Map.ExportMap(path: null, Denying(), MapWorld.None)).Value;
        Assert.Empty(_world.Exports.Documents[exported.Path].Decisions ?? []);
        await AssertNeitherSeenNorPromoted(visibleNodes: 1, decision);
    }

    // ---- overrides about nodes a promote cut (review #220 run 4 follow-up: orphaned overrides of cut nodes) ----

    private async Task<(int Orphans, int Queued)> OrphansAfterTheCut(ContextScope scope)
    {
        var queue = (await _world.Map.ReviewQueue(limit: null, scope, MapWorld.None)).Value;
        var status = (await _world.Map.MapStatus(anchor: null, scope, MapWorld.None)).Value;
        return (status.Orphaned, queue.Entries.Count(e => e.Group == ReviewGroup.Orphaned));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_override_about_a_denied_surface_or_code_node_a_promote_cut_is_cut_not_orphaned_and_blocks_nothing()
    {
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:private/a.ts\", label: \"a.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "  - {key: \"surface:setting/secret\", label: \"secret\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}",
            "edges:",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"surface:setting/secret\", to: \"feature:kept\", type: part-of, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}");
        await Decide(Drafts.Scope, On(ReviewVerb.Delete, "code:private/a.ts"));
        await Decide(Drafts.Scope, On(ReviewVerb.Reject, "surface:setting/secret"));
        await PromoteOk();
        _ = await _world.Store.OpenDraft(_world.Clock.GetUtcNow(), MapWorld.None);

        var (orphans, queued) = await OrphansAfterTheCut(Denying());
        var names = await ExportNames(Denying());
        var again = await _world.Map.Promote(Denying(), MapWorld.None);

        Assert.Equal((0, 0), (orphans, queued));
        Assert.DoesNotContain(names, n => n.Contains("private/", StringComparison.Ordinal));
        Assert.True(again.IsSuccess, again.IsError ? again.Error.Message : null);
        foreach (var reader in new[] { "show", "lenses", "neighbors", "status", "path", "query" })
        {
            Assert.Equal(ContextErrors.UnknownAnchor.Code, await Answer(reader, "code:private/a.ts"));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_override_about_a_gone_node_with_a_key_no_glob_denies_stays_an_orphan()
    {
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"feature:gone\", label: \"Gone\", provenance: model}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}");
        await Decide(Drafts.Scope, On(ReviewVerb.Accept, "feature:gone"));
        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var without = draft.Graph with { Nodes = [.. draft.Graph.Nodes.Where(n => n.Key.Value != "feature:gone")] };
        var replaced = await _world.Store.ReplaceDraft(draft.Version.Number, new MapImport(draft.Version.Repo, draft.Version.Commit, without, []), draft.Version.Created, MapWorld.None);   // a rebuild that no longer finds it
        Assert.True(replaced.IsSuccess);

        var (_, queued) = await OrphansAfterTheCut(Denying());
        var refused = await _world.Map.Promote(Denying(), MapWorld.None);

        Assert.Equal(1, queued);
        Assert.True(refused.IsError);
        Assert.Equal(ContextErrors.PromoteRefused.Code, refused.Error.Code);
        Assert.Contains("undecided", refused.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the decisions a deny cut undoes, and what it leaves (review #220 run 5) ----

    private static readonly string[] _cutNames = ["secret", "token", "Token vault", "accepted", "drafted"];

    // Decided before the glob: an accept of a kept model feature and of one only a private file implements, a reject of a private-only surface, a rename of another, and a merge
    // of a kept feature into the private-only one.
    private async Task DecidedBeforeTheGlob(params string[] extra)
    {
        await DraftOf(
            [
                "nodes:",
                "  - {key: \"feature:kept\", label: \"Kept\"}",
                "  - {key: \"feature:modelkept\", label: \"Model kept\", provenance: model}",
                "  - {key: \"feature:other\", label: \"Other\"}",
                "  - {key: \"feature:drafted\", label: \"Drafted\", provenance: model}",
                "  - {key: \"feature:accepted\", label: \"Accepted\", provenance: model}",
                "  - {key: \"code:private/a.ts\", label: \"a.ts\"}",
                "  - {key: \"code:private/c.ts\", label: \"c.ts\"}",
                "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
                "  - {key: \"surface:setting/secret\", label: \"secret\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}",
                "  - {key: \"surface:setting/token\", label: \"token\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/t.json\"}]}",
                "edges:",
                "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}",
                "  - {from: \"code:src/b.ts\", to: \"feature:modelkept\", type: implements}",
                "  - {from: \"code:src/b.ts\", to: \"feature:other\", type: implements}",
                "  - {from: \"code:private/a.ts\", to: \"feature:drafted\", type: implements}",
                "  - {from: \"code:private/c.ts\", to: \"feature:accepted\", type: implements}",
                .. extra,
            ]);
        await Decide(Drafts.Scope, On(ReviewVerb.Accept, "feature:accepted"));
        await Decide(Drafts.Scope, On(ReviewVerb.Accept, "feature:modelkept"));
        await Decide(Drafts.Scope, On(ReviewVerb.Reject, "surface:setting/secret"));
        await Decide(Drafts.Scope, new ReviewDecision(ReviewVerb.Rename, NodeKey.From("surface:setting/token"), Label: "Token vault"));
        await Decide(Drafts.Scope, On(ReviewVerb.Merge, "feature:other", other: "feature:drafted"));
    }

    private async Task PromoteOk()
    {
        var promoted = await _world.Map.Promote(Denying(), MapWorld.None);
        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
    }

    private async Task<List<string>> HistoryNames(ContextScope scope)
    {
        var history = (await _world.Map.ReviewHistory(limit: null, scope, MapWorld.None)).Value;
        return [.. history.Entries.SelectMany(e => new[] { e.Target.Value, e.Label ?? string.Empty })];
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_undoes_only_the_accepts_renames_moves_and_merges_about_what_it_cuts_and_keeps_rejects_and_deletes()
    {
        await DecidedBeforeTheGlob();

        await PromoteOk();

        var log = (await _world.Store.Overrides(MapWorld.None)).Value;
        var active = OverrideLog.Active(log);
        var undos = log.Where(o => o.Action == OverrideAction.Undo).ToList();
        Assert.All(undos, u => Assert.True(u.IsCutUndo));
        Assert.Equal("feature:accepted feature:other surface:setting/token", string.Join(' ', undos.Select(u => u.Target.Value).Order(StringComparer.Ordinal)));
        Assert.Contains(active, o => o is { Action: OverrideAction.Accept } && o.Target.Value == "feature:modelkept");
        Assert.Contains(active, o => o is { Action: OverrideAction.Reject } && o.Target.Value == "surface:setting/secret");
        Assert.DoesNotContain(active, o => new[] { "feature:accepted", "surface:setting/token", "feature:other" }.Contains(o.Target.Value, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_a_cut_promote_the_history_and_the_export_name_nothing_a_denied_file_produced()
    {
        await DecidedBeforeTheGlob();
        await PromoteOk();
        _ = await _world.Store.OpenDraft(_world.Clock.GetUtcNow(), MapWorld.None);

        var history = await HistoryNames(Denying());
        var exported = await ExportNames(Denying());
        var (orphans, queued) = await OrphansAfterTheCut(Denying());
        var again = await _world.Map.Promote(Denying(), MapWorld.None);

        Assert.Contains("feature:modelkept", history, StringComparer.Ordinal);   // a decision about what stays is still shown
        Assert.DoesNotContain(history.Concat(exported), n => _cutNames.Any(c => n.Contains(c, StringComparison.Ordinal)));
        Assert.Equal((0, 0), (orphans, queued));
        Assert.True(again.IsSuccess, again.IsError ? again.Error.Message : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reject_a_cut_promote_kept_applies_again_when_the_glob_goes_and_what_was_accepted_is_back_undecided()
    {
        await DecidedBeforeTheGlob();
        await PromoteOk();
        var draft = (await _world.Store.OpenDraft(_world.Clock.GetUtcNow(), MapWorld.None)).Value;
        var stored = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var found = stored.Graph with
        {
            Nodes = [.. stored.Graph.Nodes,
                new GraphNode(NodeKey.From("surface:setting/secret"), "secret", Provenance.Deterministic, 1, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/private/s.json"))]),
                new GraphNode(NodeKey.From("feature:accepted"), "Accepted", Provenance.Model, 0.5, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/b.ts"))])],
        };
        var replaced = await _world.Store.ReplaceDraft(draft.Number, new MapImport(draft.Repo, draft.Commit, found, []), draft.Created, MapWorld.None);   // a rebuild with the glob gone
        Assert.True(replaced.IsSuccess);

        var queue = (await _world.Map.ReviewQueue(limit: null, Drafts.Scope, MapWorld.None)).Value;
        var history = await HistoryNames(Drafts.Scope);

        Assert.DoesNotContain(queue.Entries, e => e.Node.Value == "surface:setting/secret");
        Assert.Contains(queue.Entries, e => e.Node.Value == "feature:accepted");
        Assert.Contains("surface:setting/secret", history, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_that_names_a_cut_node_by_a_stored_alias_is_undone_with_it()
    {
        await DecidedBeforeTheGlob("aliases:", "  - {alias: \"feature:old-accepted\", target: \"feature:accepted\"}");
        _ = await _world.Store.AppendOverrides([new MapOverride(OverrideAction.Accept, NodeKey.From("feature:old-accepted"), "avery", _world.Clock.GetUtcNow())], MapWorld.None);
        await PromoteOk();
        _ = await _world.Store.OpenDraft(_world.Clock.GetUtcNow(), MapWorld.None);

        var (orphans, queued) = await OrphansAfterTheCut(Denying());
        var again = await _world.Map.Promote(Denying(), MapWorld.None);

        Assert.Equal((0, 0), (orphans, queued));
        Assert.True(again.IsSuccess, again.IsError ? again.Error.Message : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_link_accept_whose_link_the_cut_removes_is_undone()
    {
        await DecidedBeforeTheGlob("  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: tests, provenance: model, evidence: [{key: \"repo:aw-split-panes/private/x.ts\"}]}");
        var link = LinkIds.For(new GraphEdge(NodeKey.From("code:src/b.ts"), NodeKey.From("feature:kept"), EdgeType.Tests, Provenance.Model));
        await Decide(Drafts.Scope, new ReviewDecision(ReviewVerb.Accept, Link: link));

        await PromoteOk();

        var log = (await _world.Store.Overrides(MapWorld.None)).Value;
        Assert.DoesNotContain(OverrideLog.Active(log), o => o.Link == link);
        _ = await _world.Store.OpenDraft(_world.Clock.GetUtcNow(), MapWorld.None);
        Assert.Equal((0, 0), await OrphansAfterTheCut(Denying()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_whose_draft_replacement_fails_leaves_neither_the_cut_draft_nor_the_undo_rows()
    {
        await DecidedBeforeTheGlob();
        var before = (await _world.Store.Overrides(MapWorld.None)).Value.Count;
        _world.Store.ReplaceDraftFailure = ContextErrors.StoreUnavailable.With("disk full");

        var refused = await _world.Map.Promote(Denying(), MapWorld.None);

        Assert.True(refused.IsError);
        Assert.Equal(before, (await _world.Store.Overrides(MapWorld.None)).Value.Count);
        Assert.Equal(1, (await _world.Store.Versions(MapWorld.None)).Value.Count(v => v.Status == MapVersionStatus.Current));
        _world.Store.ReplaceDraftFailure = null;
        await PromoteOk();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_that_fails_after_the_replacement_can_be_run_again_and_succeeds_without_orphans()
    {
        await DecidedBeforeTheGlob();
        _world.Store.PromoteFailure = ContextErrors.StoreUnavailable.With("busy");

        Assert.True((await _world.Map.Promote(Denying(), MapWorld.None)).IsError);
        _world.Store.PromoteFailure = null;
        var again = await _world.Map.Promote(Denying(), MapWorld.None);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.Message : null);
        _ = await _world.Store.OpenDraft(_world.Clock.GetUtcNow(), MapWorld.None);
        Assert.Equal((0, 0), await OrphansAfterTheCut(Denying()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reject_or_delete_of_a_node_the_map_no_longer_holds_is_satisfied_and_blocks_nothing()
    {
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"feature:gone\", label: \"Gone\", provenance: model}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}");
        await Decide(Drafts.Scope, On(ReviewVerb.Reject, "feature:gone"));
        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var without = draft.Graph with { Nodes = [.. draft.Graph.Nodes.Where(n => n.Key.Value != "feature:gone")] };
        _ = await _world.Store.ReplaceDraft(draft.Version.Number, new MapImport(draft.Version.Repo, draft.Version.Commit, without, []), draft.Version.Created, MapWorld.None);

        var (orphans, queued) = await OrphansAfterTheCut(Denying());
        var promoted = await _world.Map.Promote(Denying(), MapWorld.None);

        Assert.Equal((0, 0), (orphans, queued));
        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
    }

    // ---- a reject names the same proposal whichever came first, the glob or the reject ----

    public static TheoryData<bool> RejectedAfterTheGlob => [false, true];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(RejectedAfterTheGlob), DisableDiscoveryEnumeration = true)]
    public async Task A_rejected_proposal_with_a_denied_and_an_allowed_file_is_gone_from_the_queue_the_proposals_the_lenses_and_the_promote(bool rejectedAfterTheGlob)
    {
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:src/a.ts\", label: \"a.ts\"}",
            "edges:",
            "  - {from: \"code:src/a.ts\", to: \"feature:kept\", type: implements}");
        var mixed = Surface("Mixed", "surface:setting/mixed", "src/a.ts", "private/p.ts");
        _ = await Run("run-2", Drafts.Scope, mixed);
        await Decide(rejectedAfterTheGlob ? Denying() : Drafts.Scope, On(ReviewVerb.Reject, "surface:setting/mixed"));
        var scope = Denying();

        var queue = (await _world.Map.ReviewQueue(limit: null, scope, MapWorld.None)).Value;
        var proposals = (await _world.Map.Proposals("run-2", scope, MapWorld.None)).Value;
        var lenses = await _world.Engine.Lenses("surface:setting/mixed", read: MapRead.NewestDraft, depth: null, scope, MapWorld.None);
        var again = await Run("run-2", scope, mixed);

        Assert.Equal(0, queue.Undecided);
        Assert.Empty(queue.Entries);
        var surface = Assert.Single(proposals.Surfaces);
        Assert.Equal((GraphStatusKind.Unmapped, null), (surface.Status, surface.Node));
        Assert.Equal(ContextErrors.UnknownAnchor.Code, lenses.Error.Code);
        Assert.Equal((0, 1), (again.Added, again.Rejected));
        await AssertNeitherSeenNorPromoted(visibleNodes: 2, hidden: [], notPromoted: ["surface:setting/mixed"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_proposal_a_person_accepted_whose_only_evidence_is_denied_goes_with_it_and_does_not_block_the_promote()
    {
        await DraftOf(
            "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}");
        _ = await Run("run-3", Drafts.Scope, Surface("Hidden", "surface:setting/hidden", "private/p.ts"));
        await Decide(Drafts.Scope, On(ReviewVerb.Accept, "surface:setting/hidden"));

        var queue = (await _world.Map.ReviewQueue(limit: null, Denying(), MapWorld.None)).Value;

        Assert.Equal(0, queue.Undecided);
        Assert.Empty(queue.Entries);
        await AssertNeitherSeenNorPromoted(visibleNodes: 1, "surface:setting/hidden");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scoping_run_that_names_a_node_only_a_denied_file_evidences_proposes_it_rather_than_finding_it_existing()
    {
        await Scenario();

        var result = await Run("run-4", Denying(), Surface("Drafted", "feature:drafted", "src/b.ts"));

        Assert.Equal((1, 0), (result.Added, result.Existing));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_applied_view_is_what_its_own_graph_and_overrides_give_when_the_overrides_are_applied()
    {
        await Scenario();
        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;

        var view = MapAccess.View(draft.Graph, draft.Overrides, Denying(), pending: true, callerScope: false).Value;

        Assert.Equal(Shape(view.Applied), Shape(OverrideApplier.Apply(view.Graph, view.Overrides).Graph));
    }

    private static List<string> Shape(GraphSnapshot graph) =>
    [
        .. graph.Nodes.Select(n => $"{n.Key.Value}|{n.Provenance}|{n.Label}|{string.Join(',', n.EvidenceOrEmpty.Select(e => e.Key.Value))}").Order(StringComparer.Ordinal),
        .. graph.Edges.Select(e => $"{e.From.Value}>{e.To.Value}:{e.Type}|{e.Provenance}|{string.Join(',', e.EvidenceOrEmpty.Select(x => x.Key.Value))}").Order(StringComparer.Ordinal),
    ];
}
