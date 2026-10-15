using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
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

    private static string Code<T>(Aw.Kernel.Result<T> result) => result.IsError ? result.Error.Code : "answered";

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
}
