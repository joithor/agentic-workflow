using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>Stubs and builders the context Step input tests share.</summary>
internal static class ContextInputFixtures
{
    internal static readonly RepoName Web = RepoName.From("web");

    internal static LoadedProfile Profile() =>
        new(new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web"), new RepoEntry("api", "/work/api")]), ProfileHash.From(new string('a', 64)), "/home/profile.yaml");

    internal static LensesSetView SmallSet(params string[] anchors)
    {
        var nodes = anchors.Select(a => new LensAnchor(NodeKey.From(a), a, NodeKind.Feature, Trust.Trusted)).ToArray();
        return new LensesSetView(2, nodes, [], "v3", Draft: false, 2, DateTimeOffset.UnixEpoch,
            new LensBands(
                new IntentBand([], [], new IntentCounts(0, 0, Orphaned: null), Truncated: false),
                new ProductBand([.. nodes.Select(n => new LensItem(n.Key, n.Label, NodeKind.Feature, Provenance.Human, 1, Confirmed: true, Trust.Trusted))], [], [], new ProductCounts(nodes.Length, 0), [], Truncated: false),
                new CodeBand([], LensSymbols.Unavailable, new CodeCounts(0, 0, 0), Truncated: false)),
            [], [],
            new LensCoverage(nodes.Length, new CoverageIntent(0, nodes.Length), new CoverageProduct(nodes.Length, nodes.Length), new CoverageCode(0, nodes.Length, "file", 0), []),
            new LensSearched([], "v3", 2, [], [], new SearchedBands(new BandSearched(Empty: true), new BandSearched(Empty: false), new BandSearched(Empty: true))),
            [], new Dictionary<string, Citation>(StringComparer.Ordinal));
    }

    internal sealed class Profiles(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }
}
