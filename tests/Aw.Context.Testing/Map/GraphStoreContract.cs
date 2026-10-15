using Aw.Context.Core.Map;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>The graph store's contract (spec §6.2): the fake and the SQLite store pass the same tests.</summary>
public abstract class GraphStoreContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>An empty store has no current version.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_store_has_no_map()
    {
        const string test = nameof(An_empty_store_has_no_map);
        var store = CreateStore();

        Expect.That(Expect.Ok(await store.Current(None), test) is null, test, "a current version in an empty store");
        Expect.That(Expect.Ok(await store.Versions(None), test).Count == 0, test, "versions in an empty store");
        Expect.That(Expect.Ok(await store.Overrides(None), test).Count == 0, test, "overrides in an empty store");
    }

    /// <summary>A saved map reads back as written: evidence, aliases, decisions, attributes, timestamps and provenance.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_saved_map_is_current_and_reads_back_as_written()
    {
        const string test = nameof(A_saved_map_is_current_and_reads_back_as_written);
        var store = CreateStore();
        var map = SampleMap.Build();

        var saved = Expect.Ok(await store.SaveCurrent(map, SampleMap.At, None), test);
        var read = Expect.Ok(await store.Read(saved.Number, None), test);

        Expect.That(Expect.Ok(await store.Current(None), test)?.Number == saved.Number, test, "the saved version isn't current");
        Expect.That(saved.Repo == map.Repo && saved.Commit == map.Commit, test, "repo or commit changed");
        Expect.That(Canonical.Of(map.Graph) == Canonical.Of(read), test, $"read back differently:\n{Canonical.Of(read)}\nexpected:\n{Canonical.Of(map.Graph)}");
        Expect.That(Expect.Ok(await store.Overrides(None), test).SequenceEqual(map.Overrides), test, "overrides changed");
    }

    /// <summary>A second save retires the first; overrides are kept once.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_save_retains_the_first_and_keeps_overrides_once()
    {
        const string test = nameof(A_second_save_retains_the_first_and_keeps_overrides_once);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);

        var second = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At.AddDays(1), None), test);

        var versions = Expect.Ok(await store.Versions(None), test);
        Expect.That(versions.Select(v => (v.Number, v.Status)).SequenceEqual([(second.Number, MapVersionStatus.Current), (first.Number, MapVersionStatus.Retained)]),
            test, "expected the second current and the first retained, newest first");
        Expect.That(Expect.Ok(await store.Overrides(None), test).Count == 1, test, "the same override was kept twice");
        Expect.That(Canonical.Of(Expect.Ok(await store.Read(first.Number, None), test)) == Canonical.Of(SampleMap.Build().Graph), test, "the retained version changed");
    }

    /// <summary>Delete removes retained versions only.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Delete_removes_retained_versions_only()
    {
        const string test = nameof(Delete_removes_retained_versions_only);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var second = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At, None), test);

        var deleted = Expect.Ok(await store.Delete([first.Number, second.Number], None), test);

        Expect.That(deleted == 1, test, $"deleted {deleted.ToString(System.Globalization.CultureInfo.InvariantCulture)}, expected only the retained one");
        Expect.That(Expect.Ok(await store.Versions(None), test).Select(v => v.Number).SequenceEqual([second.Number]), test, "the current version went too");
        Expect.That(Expect.Ok(await store.Read(first.Number, None), test).Nodes.Count == 0, test, "a deleted version's graph is still there");
    }

    /// <summary>A snapshot is the current version with its graph and overrides; an empty store and a store with no draft have none.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_snapshot_is_the_current_version_with_its_graph_and_overrides()
    {
        const string test = nameof(A_snapshot_is_the_current_version_with_its_graph_and_overrides);
        var store = CreateStore();
        Expect.That(Expect.Ok(await store.Snapshot(draft: false, None), test) is null, test, "a snapshot in an empty store");
        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var second = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At.AddDays(1), None), test);

        var snapshot = Expect.Ok(await store.Snapshot(draft: false, None), test);

        Expect.That(snapshot?.Version.Number == second.Number, test, "the snapshot isn't the current version");
        Expect.That(snapshot is not null && Canonical.Of(snapshot.Graph) == Canonical.Of(SampleMap.Build(" (2)").Graph), test, "the snapshot's graph differs");
        Expect.That(snapshot?.Overrides.Count == 1, test, "the snapshot's overrides differ");
        Expect.That(Expect.Ok(await store.Snapshot(draft: true, None), test) is null, test, "a draft snapshot with no draft");
    }

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IGraphStore CreateStore();
}
