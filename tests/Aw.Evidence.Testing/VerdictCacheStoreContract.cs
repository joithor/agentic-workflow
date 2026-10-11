using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Visual;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IVerdictCacheStore contract: a saved cache loads back; a missing manifest is an empty cache.</summary>
public abstract class VerdictCacheStoreContract
{
    /// <summary>An absolute directory the store may write under, unique to the test.</summary>
    protected abstract string Root { get; }

    /// <summary>Creates the store under test.</summary>
    /// <returns>The store.</returns>
    protected abstract IVerdictCacheStore Create();

    /// <summary>Every entry (decision, reasons, run) survives save and load.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_saved_cache_loads_back()
    {
        var store = Create();
        var path = RunPath.Combine(Root, "verdict-cache.json");
        var cache = VerdictCache.Empty
            .With("k1", new VisualVerdict(VisualDecision.LooksOff, ["button overlaps"], Usage: null), Instance(1))
            .With("k2", new VisualVerdict(VisualDecision.LooksRight, [], Usage: null), Instance(2));

        Expect.That((await store.SaveAsync(path, cache, CancellationToken.None)).IsSuccess, "a save succeeds");
        var loaded = await store.LoadAsync(path, CancellationToken.None);

        Expect.That(loaded.Entries.Count == 2, "every entry loads back");
        Expect.That(loaded.Entries["k1"].Decision == VisualDecision.LooksOff, "the decision loads back");
        Expect.That(loaded.Entries["k1"].Reasons.SequenceEqual(["button overlaps"], StringComparer.Ordinal), "the reasons load back");
        Expect.That(loaded.Entries["k2"].RunInstance.Value == Instance(2).Value, "the run invocation loads back");
    }

    private static RunInstanceId Instance(int n) => RunInstanceId.From(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"instance-{n}"));

    /// <summary>Loading a manifest that was never saved is an empty cache.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_manifest_is_an_empty_cache() =>
        Expect.That((await Create().LoadAsync(RunPath.Combine(Root, "none.json"), CancellationToken.None)).Entries.Count == 0, "a missing manifest is an empty cache");

    /// <summary>Saving again replaces the earlier cache.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Saving_again_replaces_the_cache()
    {
        var store = Create();
        var path = RunPath.Combine(Root, "verdict-cache.json");
        _ = await store.SaveAsync(path, VerdictCache.Empty.With("old", new(VisualDecision.Sloppy, [], Usage: null), Instance(3)), CancellationToken.None);

        _ = await store.SaveAsync(path, VerdictCache.Empty.With("new", new(VisualDecision.LooksRight, [], Usage: null), Instance(3)), CancellationToken.None);

        Expect.That((await store.LoadAsync(path, CancellationToken.None)).Entries.Keys.SequenceEqual(["new"], StringComparer.Ordinal), "a second save replaces the cache");
    }
}
