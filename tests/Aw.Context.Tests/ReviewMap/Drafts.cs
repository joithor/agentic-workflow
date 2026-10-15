using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ReviewMap;

/// <summary>A draft for the review, promote and proposal tests: what a build of the world's fake repo writes.</summary>
internal static class Drafts
{
    public static ContextScope Scope { get; } = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    /// <summary>Builds a draft; with <paramref name="clusters"/> a cluster of two command files becomes a model feature with two suggested file links.</summary>
    public static async Task<MapBuildReport> Build(MapWorld world, bool clusters = false)
    {
        if (clusters)
        {
            world.Clusters.Set = new ClusterSet([new FileCluster(ClusterId.From("c1"), ["src/commands/ids.ts", "src/commands/registry.ts"], 0.7)], Available: true);
        }

        var built = await world.Map.BuildMap(progress: null, Scope, MapWorld.None);
        return built.IsSuccess ? built.Value : throw new InvalidOperationException(built.Error.Message);
    }

    public static async Task<ReviewQueueView> Queue(MapWorld world)
    {
        var queue = await world.Map.ReviewQueue(Scope, MapWorld.None);
        return queue.IsSuccess ? queue.Value : throw new InvalidOperationException(queue.Error.Message);
    }

    /// <summary>How answers name a draft built at the world's commit: 'v14@0a1b2c3'.</summary>
    public static string Label(MapVersion draft) => string.Create(CultureInfo.InvariantCulture, $"v{draft.Number}@{MapWorld.Head[..7]}");

    /// <summary>A version's number as an answer prints it: 'v14'.</summary>
    public static string Short(MapVersion version) => string.Create(CultureInfo.InvariantCulture, $"v{version.Number}");

    public static async Task<int> Rows(MapWorld world) => (await world.Store.Overrides(MapWorld.None)).Value.Count;
}
