using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

/// <summary>A reject's identity hashes the evidence its proposal had; after a deny glob it must still name the proposal, whether it was written before the glob (the full evidence) or after (what the glob allows) (review #220 run 4).</summary>
public sealed class RejectIdentityCutTests
{
    private static readonly NodeKey _key = NodeKey.From("surface:setting/mixed");
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly EvidenceRef[] _both =
    [
        new(RecordKey.From("repo:aw-split-panes/src/a.ts")),
        new(RecordKey.From("repo:aw-split-panes/private/p.ts")),
    ];

    private static ContextScope Denying() =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    private static GraphNode Proposed(IReadOnlyList<EvidenceRef> evidence) => new(_key, "Mixed", Provenance.Model, 0.5, evidence);

    private static bool Survives(string? rejection)
    {
        var graph = GraphSnapshot.Empty with { Nodes = [Proposed(_both)] };
        var reject = new MapOverride(OverrideAction.Reject, _key, "avery", _at, Rejection: rejection);
        var view = MapAccess.View(graph, [reject], Denying(), pending: true, callerScope: false).Value;
        return view.Applied.Resolve(_key) is not null;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reject_written_before_the_glob_still_rejects_the_node() =>
        Assert.False(Survives(RejectionIdentity.For(Proposed(_both))));

    [Fact, Trait("Lane", "unit")]
    public void A_reject_written_after_the_glob_hashes_the_allowed_evidence_and_rejects_the_node() =>
        Assert.False(Survives(RejectionIdentity.For(Proposed([_both[0]]))));

    [Fact, Trait("Lane", "unit")]
    public void A_reject_of_some_other_evidence_leaves_the_node_and_so_does_none_that_matches() =>
        Assert.True(Survives(RejectionIdentity.For(Proposed([new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/other.ts"))]))));

    [Fact, Trait("Lane", "unit")]
    public void A_reject_with_no_identity_rejects_whatever_the_node_is() =>
        Assert.False(Survives(rejection: null));
}
