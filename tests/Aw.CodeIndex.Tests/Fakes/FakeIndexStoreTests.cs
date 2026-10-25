using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Fakes;

public sealed class FakeIndexStoreTests
{
    private static readonly RepoName _repo = RepoName.From("fixture");
    private static readonly CommitSha _commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");
    private static readonly DateTimeOffset _builtAt = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_with_no_build_has_no_header_and_no_reader()
    {
        var store = new FakeIndexStore();

        Assert.Null((await store.ReadHeader(_repo, TestContext.Current.CancellationToken)).Value);
        Assert.Equal("SND-CODEIDX-013", (await store.OpenRead(_repo, TestContext.Current.CancellationToken)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_published_build_is_read_back_with_counts_the_store_made()
    {
        var store = new FakeIndexStore();
        await Publish(store, SymbolsIn("src/A.cs", "Run", "Stop"));

        var header = (await store.ReadHeader(_repo, TestContext.Current.CancellationToken)).Value!;

        Assert.Equal(_commit, header.Meta.Commit);
        Assert.Equal(new IndexCounts(Files: 1, Symbols: 2, Edges: 0), header.Meta.Counts);
        Assert.Equal(CodeIndexRules.SchemaVersion, header.Meta.Schema);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_disposed_without_publishing_leaves_the_live_index_alone()
    {
        var store = new FakeIndexStore();
        await Publish(store, SymbolsIn("src/A.cs", "Run"));

        var build = (await store.BeginBuild(_repo, incremental: true, TestContext.Current.CancellationToken)).Value;
        _ = await build.WriteFiles([File("src/B.cs", "Other")], TestContext.Current.CancellationToken);
        await build.DisposeAsync();

        var reader = (await store.OpenRead(_repo, TestContext.Current.CancellationToken)).Value;
        var page = (await reader.FindSymbols(new SymbolQuery("Other", SymbolMatch.Exact, Repo: null), TestContext.Current.CancellationToken)).Value;
        Assert.Empty(page.Hits);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_incremental_build_starts_from_a_copy_of_the_live_index()
    {
        var store = new FakeIndexStore();
        await Publish(store, SymbolsIn("src/A.cs", "Run"));

        var build = (await store.BeginBuild(_repo, incremental: true, TestContext.Current.CancellationToken)).Value;

        Assert.True(build.Incremental);
        Assert.Equal(_commit, build.Previous!.Meta.Commit);
        var stored = (await build.StoredFiles(TestContext.Current.CancellationToken)).Value;
        Assert.Equal("src/A.cs", Assert.Single(stored).Path);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_damaged_index_reads_as_damaged_and_a_build_starts_over()
    {
        var store = new FakeIndexStore();
        await Publish(store, SymbolsIn("src/A.cs", "Run"));
        store.Damage(_repo);

        Assert.Equal("SND-CODEIDX-010", (await store.ReadHeader(_repo, TestContext.Current.CancellationToken)).Error.Code);
        var build = (await store.BeginBuild(_repo, incremental: true, TestContext.Current.CancellationToken)).Value;
        Assert.False(build.Incremental);
        Assert.Null(build.Previous);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Removing_a_file_takes_its_symbols_and_the_edges_into_them()
    {
        var store = new FakeIndexStore();
        var caller = Symbol("src/A.cs", "Run");
        var callee = Symbol("src/B.cs", "Helper");
        var build = (await store.BeginBuild(_repo, incremental: false, TestContext.Current.CancellationToken)).Value;
        _ = await build.WriteFiles([new FileRecord("src/A.cs", "a1", 10, "csharp", Parsed: true, [caller]), new FileRecord("src/B.cs", "b1", 10, "csharp", Parsed: true, [callee])], TestContext.Current.CancellationToken);
        _ = await build.AddEdges([new EdgeRecord(caller.Ref, callee.Ref, "Helper", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 3, Candidates: 1)], TestContext.Current.CancellationToken);

        _ = await build.RemoveFiles(["src/B.cs"], TestContext.Current.CancellationToken);
        _ = await build.Publish(Meta(), TestContext.Current.CancellationToken);

        var reader = (await store.OpenRead(_repo, TestContext.Current.CancellationToken)).Value;
        var walk = (await reader.Walk(caller.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, TestContext.Current.CancellationToken)).Value;
        var edge = Assert.Single(walk.Edges);
        Assert.Null(edge.Other);
        Assert.Equal("Helper", edge.Name);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Callers_walk_the_edges_into_a_symbol()
    {
        var store = new FakeIndexStore();
        var caller = Symbol("src/A.cs", "Run");
        var callee = Symbol("src/B.cs", "Helper");
        var build = (await store.BeginBuild(_repo, incremental: false, TestContext.Current.CancellationToken)).Value;
        _ = await build.WriteFiles([new FileRecord("src/A.cs", "a1", 10, "csharp", Parsed: true, [caller]), new FileRecord("src/B.cs", "b1", 10, "csharp", Parsed: true, [callee])], TestContext.Current.CancellationToken);
        _ = await build.AddEdges([new EdgeRecord(caller.Ref, callee.Ref, "Helper", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 3, Candidates: 1)], TestContext.Current.CancellationToken);
        _ = await build.Publish(Meta(), TestContext.Current.CancellationToken);

        var reader = (await store.OpenRead(_repo, TestContext.Current.CancellationToken)).Value;
        var walk = (await reader.Walk(callee.Ref, CallDirection.Callers, CallDepth.One, includeImplementations: false, TestContext.Current.CancellationToken)).Value;

        var edge = Assert.Single(walk.Edges);
        Assert.Equal(caller.Ref, edge.Other!.Ref);
        Assert.Equal(Confidence.Certain, edge.Confidence);
        Assert.Equal("SND-CODEIDX-011", (await reader.Walk(SymbolRef.From("fixture:nope.cs#Nope"), CallDirection.Callers, CallDepth.One, includeImplementations: false, TestContext.Current.CancellationToken)).Error.Code);
    }

    private static IndexMeta Meta() => new(_commit, "main", _builtAt, CodeIndexRules.SchemaVersion, "parser-1", LockSha: null, new IndexCounts(0, 0, 0));

    private static SymbolRecord Symbol(string file, string name)
        => new(SymbolRef.From($"fixture:{file}#{name}"), name, name, SymbolKind.Method, Arity: 0, StartLine: 1, EndLine: 5, Exported: true, Utility: false, $"void {name}()", "shape", TokenCount: 10, Complexity: 1, MinHash: default, [], Body: null, []);

    private static FileRecord File(string path, string name) => new(path, $"blob-{path}", 10, "csharp", Parsed: true, [Symbol(path, name)]);

    private static FileRecord[] SymbolsIn(string path, params string[] names)
        => [new FileRecord(path, $"blob-{path}", 10, "csharp", Parsed: true, [.. names.Select(n => Symbol(path, n))])];

    private static async Task Publish(FakeIndexStore store, FileRecord[] files)
    {
        var build = (await store.BeginBuild(_repo, incremental: false, TestContext.Current.CancellationToken)).Value;
        _ = await build.WriteFiles(files, TestContext.Current.CancellationToken);
        _ = await build.Publish(Meta(), TestContext.Current.CancellationToken);
    }
}
