using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
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
            "lenses" => Code(await _world.Engine.Lenses(key, draft: false, depth: null, scope, MapWorld.None)),
            "lenses-draft" => Code(await _world.Engine.Lenses(key, draft: true, depth: null, scope, MapWorld.None)),
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
        Assert.Empty(seen["proposals"]);   // the one surface the run proposed has only denied evidence: it reads as unmapped
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
        var lenses = (await _world.Engine.Lenses("feature:kept", draft: false, depth: null, scope, MapWorld.None)).Value;
        var exported = (await _world.Map.ExportMap(path: null, scope, MapWorld.None)).Value;
        var document = _world.Exports.Documents[exported.Path];

        Assert.Equivalent(lenses.Coverage, status.Coverage);
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
    private async Task AssertNeitherSeenNorPromoted(int visibleNodes, params string[] hidden)
    {
        var scope = Denying();
        var names = await ExportNames(scope);
        var status = (await _world.Map.MapStatus(anchor: null, scope, MapWorld.None)).Value;
        Assert.DoesNotContain(names, n => n.Contains("private/", StringComparison.Ordinal) || hidden.Contains(n, StringComparer.Ordinal));
        Assert.Equal(visibleNodes, status.Counts.Nodes);

        var promoted = await _world.Map.Promote(scope, MapWorld.None);

        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        var stored = await StoredNames();
        Assert.DoesNotContain(stored, n => n.Contains("private/", StringComparison.Ordinal) || hidden.Contains(n, StringComparer.Ordinal));
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

        var other = (await _world.Engine.Lenses("feature:other", draft: false, depth: null, Denying(), MapWorld.None)).Value;

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
        var before = (await _world.Engine.Lenses("feature:kept", draft: false, depth: null, Drafts.Scope, MapWorld.None)).Value;

        var lenses = (await _world.Engine.Lenses("feature:kept", draft: false, depth: null, Denying(), MapWorld.None)).Value;
        var query = (await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:kept"), Denying(), MapWorld.None)).Value;

        _ = Assert.Single(before.Bands.Intent.Decisions);
        Assert.Empty(lenses.Bands.Intent.Decisions);
        Assert.DoesNotContain(quote, string.Join('\n', query.Sections.SelectMany(s => s.Items).SelectMany(i => new[] { i.Title, i.Text })), StringComparison.Ordinal);
        var exported = (await _world.Map.ExportMap(path: null, Denying(), MapWorld.None)).Value;
        Assert.Empty(_world.Exports.Documents[exported.Path].Decisions ?? []);
        await AssertNeitherSeenNorPromoted(visibleNodes: 1, decision);
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
        var lenses = await _world.Engine.Lenses("surface:setting/mixed", draft: true, depth: null, scope, MapWorld.None);
        var again = await Run("run-2", scope, mixed);

        Assert.Equal(0, queue.Undecided);
        Assert.Empty(queue.Entries);
        var surface = Assert.Single(proposals.Surfaces);
        Assert.Equal((GraphStatusKind.Unmapped, null), (surface.Status, surface.Node));
        Assert.Equal(ContextErrors.UnknownAnchor.Code, lenses.Error.Code);
        Assert.Equal((0, 1), (again.Added, again.Rejected));
        await AssertNeitherSeenNorPromoted(visibleNodes: 2, "surface:setting/mixed");
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
