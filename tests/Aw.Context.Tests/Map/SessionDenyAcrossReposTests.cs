using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.Map;

/// <summary>Review #234 run 1 (security-sentinel-0): the deny cut uses every profile repo's globs for every caller, so a session in repo A never reaches a document repo B denies, nor a decision quoting it.</summary>
public sealed class SessionDenyAcrossReposTests : IDisposable
{
    private const string Other = "other";
    private const string PrivateDoc = "repo:other/docs/private/x.md";
    private const string PublicDoc = "repo:other/docs/public/ok.md";
    private const string PrivateQuote = "BPRIVATE-QUOTE-ZEBRA";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static string PrivateDecision => DecisionRow.KeyFor(RecordKey.From(PrivateDoc), PrivateQuote).Value;

    private static IReadOnlyList<ContextRepo> Repos(bool otherDenies) =>
    [
        new(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes"),
        new(ContextRepoName.From(Other), "/work/other") { DenyPaths = [.. PathDenylist.Defaults, .. otherDenies ? new[] { "docs/private/**" } : []] },
    ];

    private static ContextScope Scope(ContextCallerKind kind, bool otherDenies, bool narrowed = false)
    {
        using var section = System.Text.Json.JsonDocument.Parse(narrowed
            ? """{"sessions":{"enabled":true},"repos":["aw-split-panes"]}"""
            : """{"sessions":{"enabled":true}}""");
        var caller = kind == ContextCallerKind.Session
            ? new ContextCaller(kind, ContextRepoName.From(FixtureMap.Repo), ContextClient.Http)
            : new ContextCaller(kind, Repo: null, ContextClient.Cli);
        return new(section.RootElement.Clone(), Repos(otherDenies), caller);
    }

    private async Task ImportMap() => _ = await _world.ImportYaml(MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"feature:in\", label: \"In scope\"}",
        "  - {key: \"code:src/in.ts\", label: \"in.ts\", provenance: deterministic}",
        $"  - {{key: \"record:{PrivateDoc}\", label: \"BPRIVATE-TITLE-ZEBRA\"}}",
        $"  - {{key: \"record:{PublicDoc}\", label: \"Public note\"}}",
        "edges:",
        "  - {from: \"code:src/in.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in.ts\"}]}",
        $"  - {{from: \"record:{PrivateDoc}\", to: \"feature:in\", type: mentions}}",
        $"  - {{from: \"record:{PublicDoc}\", to: \"feature:in\", type: mentions}}",
        $"  - {{from: \"{PrivateDecision}\", to: \"feature:in\", type: decides}}",
        "decisions:",
        $"  - {{record: \"{PrivateDoc}\", quote: \"{PrivateQuote}\", wording: \"BPRIVATE-WORDING-ZEBRA\", at: 2026-07-15T10:00:00Z}}"));

    /// <summary>Everything a caller can read about feature:in and the private pair, as one string.</summary>
    private async Task<(string Text, string[] Codes)> Reads(ContextScope scope)
    {
        var ct = MapWorld.None;
        var text = new System.Text.StringBuilder();
        var codes = new List<string>();

        void Take(Result<string> r) { if (r.IsError) { codes.Add(r.Error.Code); text.Append(r.Error.Message); } else { text.Append(r.Value); } }

        var lens = await _world.Engine.Lenses("feature:in", read: MapRead.Current, depth: 3, scope, ct);
        Take(lens.Map(l => string.Join(' ', l.Bands.Intent.Records.Select(r => r.Key.Value + r.Label)) + string.Join(' ', l.Bands.Intent.Decisions.Select(d => d.Node.Value + d.Text + d.Quote?.Text))));
        var query = await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:in"), scope, ct);
        Take(query.Map(q => string.Join(' ', q.Sections.SelectMany(s => s.Items).Select(i => i.Node + i.Title + i.Text))));
        var neighbors = await _world.Engine.Neighbors("feature:in", depth: 3, scope, ct);
        Take(neighbors.Map(n => string.Join(' ', n.Nodes.Select(x => x.Node.Key.Value + x.Node.Label)) + string.Join(' ', n.Edges.Select(e => e.From.Value + e.To.Value))));
        foreach (var key in new[] { "record:" + PrivateDoc, PrivateDecision })
        {
            Take((await _world.Engine.ShowNode(key, scope, ct)).Map(n => n.Node.Label));
            Take((await _world.Engine.Path("feature:in", key, scope, ct)).Map(p => string.Join(' ', p.Steps.Select(s => s.Node.Key.Value + s.Node.Label))));
        }

        return (text.ToString(), [.. codes]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ContextCallerKind.Session, false)]
    [InlineData(ContextCallerKind.Human, false)]
    [InlineData(ContextCallerKind.Session, true)]
    [InlineData(ContextCallerKind.Human, true)]
    public async Task A_document_another_profile_repo_denies_is_gone_with_its_decision_for_a_session_and_a_human_even_when_context_repos_leaves_that_repo_out(ContextCallerKind kind, bool narrowed)
    {
        await ImportMap();

        var (open, _) = await Reads(Scope(kind, otherDenies: false, narrowed));
        var (denied, codes) = await Reads(Scope(kind, otherDenies: true, narrowed));

        Assert.Contains("BPRIVATE-TITLE-ZEBRA", open, StringComparison.Ordinal);   // why: without the glob the pair is visible, so the denied run proves the cut
        Assert.DoesNotContain("ZEBRA", denied, StringComparison.Ordinal);
        Assert.Contains("Public note", denied, StringComparison.Ordinal);
        Assert.Equal(["SND-CONTEXT-003", "SND-CONTEXT-003", "SND-CONTEXT-003", "SND-CONTEXT-003"], codes);
    }
}
