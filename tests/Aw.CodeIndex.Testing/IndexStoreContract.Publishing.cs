using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Xunit;

namespace Aw.CodeIndex.Testing;

public abstract partial class IndexStoreContract
{
    /// <summary>A repo with no build has no header, and opening it is SND-CODEIDX-013.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_repo_with_no_build_has_no_header_and_no_reader()
    {
        var store = Create();

        Expect.That(Ok(await store.ReadHeader(Repo, _none), "reading a missing header") is null, "a repo with no build has no header");
        Fails(await store.OpenRead(Repo, _none), "SND-CODEIDX-013", "opening a missing index");
    }

    /// <summary>A published build's header comes back with the meta it was given and counts the store made.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_published_build_reads_back_its_header_with_counts_the_store_made()
    {
        var store = Create();
        var run = SymbolOf("src/A.cs", "A.Run");
        var published = await Publish(store, [FileOf("src/A.cs", run, SymbolOf("src/A.cs", "A.Stop")), FileOf("src/B.cs", SymbolOf("src/B.cs", "B.Go"))], CallEdge(run, SymbolOf("src/B.cs", "B.Go")));

        var header = Ok(await store.ReadHeader(Repo, _none), "reading the header")!;

        Expect.That(header.Meta.Commit == Commit && header.Meta.DefaultBranch == "main" && header.Meta.BuiltAt == BuiltAt, "the commit, branch and time are the build's");
        Expect.That(header.Meta.ParserVersion == "parser-1" && header.Meta.LockSha == "lock-1" && header.Meta.Schema == CodeIndexRules.SchemaVersion, "the parser, lock and schema are stored");
        Expect.That(header.Meta.Counts == new IndexCounts(Files: 2, Symbols: 3, Edges: 1), "the store counts files, symbols and edges itself");
        Expect.That(published.Meta.Counts == header.Meta.Counts, "Publish returns the header that is read back");
    }

    /// <summary>A lock sha left out reads back as none.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_lock_sha_reads_back_as_none()
    {
        var store = Create();
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.Publish(Meta() with { LockSha = null }, _none), "publishing an empty build");
        }

        Expect.That(Ok(await store.ReadHeader(Repo, _none), "reading the header")!.Meta.LockSha is null, "no lock sha reads back as null");
    }

    /// <summary>Layer rows set during the build are in the header, in layer order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Layer_rows_are_in_the_header_in_layer_order()
    {
        var store = Create();
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.SetLayer(new LayerRecord(LayerName.Graph, "g:1", LayerState.Unavailable, "SND-CODEIDX-008", "graphify is missing", BuiltAt), _none), "setting a layer");
            Succeeds(await build.SetLayer(new LayerRecord(LayerName.Structure, "p:1", LayerState.Ok, Code: null, "", BuiltAt), _none), "setting a layer");
            Succeeds(await build.SetLayer(new LayerRecord(LayerName.Structure, "p:2", LayerState.Ok, Code: null, "", BuiltAt), _none), "setting a layer again replaces it");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        var layers = Ok(await store.ReadHeader(Repo, _none), "reading the header")!.Layers;

        Expect.That(layers.Select(l => (l.Layer, l.Stamp)).SequenceEqual([(LayerName.Structure, "p:2"), (LayerName.Graph, "g:1")]), "the layers are the last rows set, structure first");
        Expect.That(layers[1] is { State: LayerState.Unavailable } && string.Equals(layers[1].Code, "SND-CODEIDX-008", StringComparison.Ordinal) && string.Equals(layers[1].Detail, "graphify is missing", StringComparison.Ordinal), "a layer keeps its state, code and detail");
    }

    /// <summary>A second publish replaces the first; the old index is not kept.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_second_publish_replaces_the_first()
    {
        var store = Create();
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run"))]);
        var second = CommitSha.From(new string('b', 40));
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/B.cs", SymbolOf("src/B.cs", "B.Go"))], _none), "writing");
            Succeeds(await build.Publish(Meta(second), _none), "publishing");
        }

        var reader = await Reader(store);
        await using (reader)
        {
            Expect.That(reader.Header.Meta.Commit == second, "the live index describes the new commit");
            var page = Ok(await reader.FindSymbols(new SymbolQuery("Run", SymbolMatch.Exact, Repo: null), _none), "finding");
            Expect.That(page.Hits.Count == 0, "a non-incremental build keeps nothing of the old index");
        }
    }

    /// <summary>A reader opened before a publish keeps reading the index it opened.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_reader_opened_before_a_publish_keeps_reading_the_old_index()
    {
        var store = Create();
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run"))]);
        var reader = await Reader(store);
        await using (reader)
        {
            _ = await Publish(store, [FileOf("src/B.cs", SymbolOf("src/B.cs", "B.Go"))]);

            var old = Ok(await reader.FindSymbols(new SymbolQuery("Run", SymbolMatch.Exact, Repo: null), _none), "finding in the old index");
            Expect.That(old.Hits.Count == 1, "the open reader still sees the old index");
        }

        var fresh = await Reader(store);
        await using (fresh)
        {
            Expect.That(Ok(await fresh.FindSymbols(new SymbolQuery("Go", SymbolMatch.Exact, Repo: null), _none), "finding").Hits.Count == 1, "a new reader sees the new index");
        }
    }

    /// <summary>A build disposed without publishing leaves the live index alone.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_build_disposed_without_publishing_leaves_the_live_index_alone()
    {
        var store = Create();
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run"))]);

        var build = await Begin(store, incremental: true);
        Succeeds(await build.WriteFiles([FileOf("src/B.cs", SymbolOf("src/B.cs", "B.Other"))], _none), "writing");
        await build.DisposeAsync();

        await ExpectOnlyRun(store);
    }

    /// <summary>A build abandoned unpublished (its process died, which also drops the repo's lock; here it is disposed) leaves the live index alone.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_build_that_is_abandoned_leaves_the_live_index_alone()
    {
        var store = Create();
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run"))]);

        var abandoned = await Begin(store, incremental: true);
        Succeeds(await abandoned.WriteFiles([FileOf("src/B.cs", SymbolOf("src/B.cs", "B.Other"))], _none), "writing");

        await abandoned.DisposeAsync();
        await ExpectOnlyRun(store);
        var next = await Begin(store, incremental: true);
        await using (next)
        {
            Succeeds(await next.Publish(Meta(), _none), "a later build still publishes");
        }

        await ExpectOnlyRun(store);
    }

    /// <summary>An incremental build starts from a copy of the live index and shows its header; any other build starts empty.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_incremental_build_starts_from_a_copy_of_the_live_index()
    {
        var store = Create();
        var first = await Begin(store, incremental: true);
        await using (first)
        {
            Expect.That(!first.Incremental && first.Previous is null, "with no live index even an incremental request starts from scratch");
            Succeeds(await first.WriteFiles([FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run"))], _none), "writing");
            Succeeds(await first.Publish(Meta(), _none), "publishing");
        }

        var copy = await Begin(store, incremental: true);
        await using (copy)
        {
            Expect.That(copy.Incremental && copy.Previous?.Meta.Commit == Commit, "an incremental build names the header it copied");
            var stored = Ok(await copy.StoredFiles(_none), "reading stored files");
            Expect.That(stored.Select(f => f.Path).SequenceEqual(["src/A.cs"], StringComparer.Ordinal) && stored[0].Blob == "blob-src/A.cs" && stored[0].Parsed, "the copy holds the live files");
        }

        var scratch = await Begin(store, incremental: false);
        await using (scratch)
        {
            Expect.That(!scratch.Incremental && scratch.Previous is null, "a non-incremental build starts empty");
            Expect.That(Ok(await scratch.StoredFiles(_none), "reading stored files").Count == 0, "and holds no files");
        }
    }

    /// <summary>A damaged index reads as damaged, a build starts over, and the next publish repairs it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_damaged_index_reads_as_damaged_and_a_build_starts_over()
    {
        var store = Create();
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run"))]);
        Corrupt(store, Repo);

        Fails(await store.ReadHeader(Repo, _none), "SND-CODEIDX-010", "reading a damaged header");
        Fails(await store.OpenRead(Repo, _none), "SND-CODEIDX-010", "opening a damaged index");
        var build = await Begin(store, incremental: true);
        await using (build)
        {
            Expect.That(!build.Incremental && build.Previous is null, "a build over a damaged index starts from scratch");
            Succeeds(await build.Publish(Meta(), _none), "and publishes");
        }

        Expect.That(Ok(await store.ReadHeader(Repo, _none), "reading the repaired header")!.Meta.Commit == Commit, "the next publish repairs the index");
    }

    /// <summary>Sweeping a repo with nothing to sweep succeeds and keeps the live index.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Sweeping_keeps_the_live_index()
    {
        var store = Create();
        Succeeds(await store.Sweep(Repo, _none), "sweeping a repo with no files");
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run"))]);

        Succeeds(await store.Sweep(Repo, _none), "sweeping");

        await ExpectOnlyRun(store);
    }

    private static async Task ExpectOnlyRun(IIndexStore store)
    {
        var reader = await Reader(store);
        await using (reader)
        {
            var all = Ok(await reader.FileSymbols(["src/A.cs", "src/B.cs"], _none), "listing the files' symbols");
            Expect.That(all.Select(e => e.Symbols.Count).SequenceEqual([1, 0]), "the live index holds only what it was published with");
        }
    }
}
