using System.Diagnostics;
using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.PromoteMap;

/// <summary>A promote at the size of the owner's draft: every proposal decided, thousands of accepted scoping proposals.</summary>
public sealed class PromoteAtSizeTests : IDisposable
{
    private const int Features = 20_000, Files = 4_473, Proposals = 5_000;

    private static readonly EvidenceRef[] _evidence = [new(RecordKey.From("linear:issue/ENG-1"), "span-a")];

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static string F(int i) => i.ToString("d5", CultureInfo.InvariantCulture);

    private static NodeKey Key(string value) => NodeKey.From(value);

    [Fact, Trait("Lane", "heavy")]
    public async Task A_promote_at_the_size_of_the_owners_draft_takes_seconds()
    {
        var at = SampleDrafts.At;
        var nodes = Enumerable.Range(0, Features).Select(i => new GraphNode(Key("feature:f" + F(i)), F(i), Provenance.Model, 0.5))
            .Concat(Enumerable.Range(0, Files).Select(i => new GraphNode(Key("code:src/c" + F(i) + ".ts"), F(i), Provenance.Human, 1))).ToList();
        var links = Enumerable.Range(0, Features).Select(i => new GraphEdge(Key("code:src/c" + F(i % Files) + ".ts"), Key("feature:f" + F(i)), EdgeType.Implements, Provenance.Model, 0.5)).ToList();
        var parts = Enumerable.Range(1, 13_778).Select(i => new GraphEdge(Key("feature:f" + F(i)), Key("feature:f" + F(i / 2)), EdgeType.PartOf, Provenance.Deterministic, 1)).ToList();
        _ = await _world.Store.SaveDraft(new MapImport(ContextRepoName.From(FixtureMap.Repo), "abc123", new GraphSnapshot(nodes, [.. links, .. parts], [], []), []), at, MapWorld.None);
        var proposed = Enumerable.Range(0, Proposals).Select(i => new MapOverride(OverrideAction.Propose, Key("feature:p" + F(i)), "service:engine", at.AddMinutes(1), "P" + F(i),
            Proposal: new ProposalPayload("run-1", ContextRepoName.From(FixtureMap.Repo), NodeKind.Feature, Key("feature:f" + F(i)), _evidence, "why"))).ToList();
        var decided = nodes.Take(Features).Select(n => new MapOverride(OverrideAction.Accept, n.Key, "avery", at.AddMinutes(2)))
            .Concat(links.Select(e => new MapOverride(OverrideAction.Accept, e.From, "avery", at.AddMinutes(2), Link: LinkIds.For(e))))
            .Concat(proposed.Select(p => new MapOverride(OverrideAction.Accept, p.Target, "avery", at.AddMinutes(3))));
        _ = await _world.Store.AppendOverrides([.. proposed, .. decided], MapWorld.None);
        var clock = Stopwatch.StartNew();

        var promoted = await _world.Map.Promote(MapWorld.Human, MapWorld.None);

        clock.Stop();
        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), string.Create(CultureInfo.InvariantCulture, $"the promote took {clock.Elapsed.TotalSeconds:F1} s"));
    }
}
