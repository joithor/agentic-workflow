using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Xunit;

namespace Aw.CodeIndex.Testing;

public abstract partial class IndexStoreContract
{
    /// <summary>Written files read back by file, symbols in line order; a path with no file has no symbols.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Written_files_list_their_symbols_in_line_order()
    {
        var store = Create();
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Late", line: 20), SymbolOf("src/A.cs", "A.Early", line: 2))]);

        var reader = await Reader(store);
        await using (reader)
        {
            var entries = Ok(await reader.FileSymbols(["src/A.cs", "src/none.cs"], _none), "listing");

            Expect.That(entries.Select(e => e.Path).SequenceEqual(["src/A.cs", "src/none.cs"], StringComparer.Ordinal), "one entry per path asked, in the order asked");
            Expect.That(entries[0].Symbols.Select(s => s.Name).SequenceEqual(["Early", "Late"], StringComparer.Ordinal), "symbols come in line order");
            Expect.That(entries[1].Symbols.Count == 0, "a path with no file has an empty entry");
            Expect.That(entries[0].Symbols[0] is { Kind: SymbolKind.Method, StartLine: 2, EndLine: 6, Exported: true } && string.Equals(entries[0].Symbols[0].File, "src/A.cs", StringComparison.Ordinal) && string.Equals(entries[0].Symbols[0].Signature, "void Early()", StringComparison.Ordinal), "a hit carries kind, file, span, signature and exported");
        }
    }

    /// <summary>Writing a file again replaces its symbols; the batch is atomic per call.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Writing_a_file_again_replaces_its_symbols()
    {
        var store = Create();
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Old"))], _none), "the first write");
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", SymbolOf("src/A.cs", "A.New"))], _none), "the second write");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        var reader = await Reader(store);
        await using (reader)
        {
            var entry = Ok(await reader.FileSymbols(["src/A.cs"], _none), "listing")[0];
            Expect.That(entry.Symbols.Select(s => s.Name).SequenceEqual(["New"], StringComparer.Ordinal), "only the second write's symbols remain");
        }
    }

    /// <summary>The stored file rows list path, blob and parsed flag in path order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Stored_files_list_path_blob_and_parsed_in_path_order()
    {
        var store = Create();
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/B.cs"), FileOf("src/A.cs") with { Blob = "b1", Parsed = false }], _none), "writing");

            var stored = Ok(await build.StoredFiles(_none), "reading");

            Expect.That(stored.SequenceEqual([new StoredFile("src/A.cs", "b1", Parsed: false), new StoredFile("src/B.cs", "blob-src/B.cs", Parsed: true)]), "rows come back as written, in path order");
        }
    }

    /// <summary>Removing a file takes its symbols and leaves the edges into them unresolved, with their names.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Removing_a_file_takes_its_symbols_and_unresolves_the_edges_into_them()
    {
        var store = Create();
        var caller = SymbolOf("src/A.cs", "A.Run");
        var callee = SymbolOf("src/B.cs", "B.Helper");
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", caller), FileOf("src/B.cs", callee)], _none), "writing");
            Succeeds(await build.AddEdges([CallEdge(caller, callee)], _none), "adding the edge");
            Succeeds(await build.RemoveFiles(["src/B.cs"], _none), "removing");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        var reader = await Reader(store);
        await using (reader)
        {
            var walk = Ok(await reader.Walk(caller.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, _none), "walking");
            var edge = Only(walk.Edges);
            Expect.That(edge.Other is null && edge.Name == "Helper", "the edge keeps its callee name and loses its target");
            Expect.That(Ok(await reader.FileSymbols(["src/B.cs"], _none), "listing")[0].Symbols.Count == 0, "the removed file has no symbols");
        }
    }

    /// <summary>Rewriting a file drops the edges out of its old symbols and unresolves the edges into them.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Rewriting_a_file_drops_edges_out_of_it_and_unresolves_edges_into_it()
    {
        var store = Create();
        var a = SymbolOf("src/A.cs", "A.Run");
        var b = SymbolOf("src/B.cs", "B.Helper");
        var c = SymbolOf("src/C.cs", "C.Go");
        _ = await Publish(store, [FileOf("src/A.cs", a), FileOf("src/B.cs", b), FileOf("src/C.cs", c)], CallEdge(a, b), CallEdge(b, c));

        var build = await Begin(store, incremental: true);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/B.cs", b) with { Blob = "changed" }], _none), "rewriting B");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        var reader = await Reader(store);
        await using (reader)
        {
            var fromA = Ok(await reader.Walk(a.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, _none), "walking from A");
            Expect.That(Only(fromA.Edges) is { Other: null } edgeIn && string.Equals(edgeIn.Name, "Helper", StringComparison.Ordinal), "the edge into the rewritten file is unresolved until the next resolve");
            var fromB = Ok(await reader.Walk(b.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, _none), "walking from B");
            Expect.That(fromB.Edges.Count == 0, "the edges out of the rewritten file's old symbols are gone");
        }
    }

    /// <summary>Two ambiguous edges that differ only in their target collapse to one unresolved edge when both targets are rewritten.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Unresolving_two_ambiguous_edges_leaves_one()
    {
        var store = Create();
        var caller = SymbolOf("src/A.cs", "A.Run");
        var first = SymbolOf("src/B.cs", "B.Handle");
        var second = SymbolOf("src/C.cs", "C.Handle");
        EdgeRecord ambiguous(SymbolRecord to) => CallEdge(caller, to, line: 7) with { Confidence = Confidence.Ambiguous, Candidates = 2 };
        _ = await Publish(store, [FileOf("src/A.cs", caller), FileOf("src/B.cs", first), FileOf("src/C.cs", second)], ambiguous(first), ambiguous(second));

        var build = await Begin(store, incremental: true);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/B.cs", first), FileOf("src/C.cs", second)], _none), "rewriting both targets");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        var reader = await Reader(store);
        await using (reader)
        {
            var walk = Ok(await reader.Walk(caller.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, _none), "walking");
            Expect.That(Only(walk.Edges) is { Other: null, Line: 7 } edgeOut && string.Equals(edgeOut.Name, "Handle", StringComparison.Ordinal), "one unresolved edge is left");
        }
    }

    /// <summary>An edge identical to a stored one in source, name, kind, basis, line and target is ignored, so the first stays.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_identical_edge_is_ignored()
    {
        var store = Create();
        var caller = SymbolOf("src/A.cs", "A.Run");
        var callee = SymbolOf("src/B.cs", "B.Helper");
        var header = await Publish(store, [FileOf("src/A.cs", caller), FileOf("src/B.cs", callee)], CallEdge(caller, callee), CallEdge(caller, callee) with { Confidence = Confidence.Likely }, CallEdge(caller, callee, line: 4));

        var reader = await Reader(store);
        await using (reader)
        {
            var walk = Ok(await reader.Walk(caller.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, _none), "walking");
            Expect.That(header.Meta.Counts.Edges == 2 && walk.Edges.Select(e => (e.Line, e.Confidence)).SequenceEqual([(3, Confidence.Certain), (4, Confidence.Certain)]), "the repeat on the same line is dropped, a call on another line is kept");
        }
    }

    /// <summary>Clearing the edges of one basis leaves the other's.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Clearing_edges_removes_only_that_basis()
    {
        var store = Create();
        var caller = SymbolOf("src/A.cs", "A.Run");
        var callee = SymbolOf("src/B.cs", "B.Helper");
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", caller), FileOf("src/B.cs", callee)], _none), "writing");
            Succeeds(await build.AddEdges([CallEdge(caller, callee), CallEdge(caller, callee, line: 9) with { Basis = EdgeBasis.Graphify }], _none), "adding edges");
            Succeeds(await build.ClearEdges(EdgeBasis.Syntax, _none), "clearing the syntax edges");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }

        var reader = await Reader(store);
        await using (reader)
        {
            var walk = Ok(await reader.Walk(caller.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, _none), "walking");
            Expect.That(Only(walk.Edges).Basis == EdgeBasis.Graphify, "only the graphify edge is left");
        }
    }

    /// <summary>The resolver's inputs: every symbol, and every call site with its caller.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_resolver_reads_every_symbol_and_call_site()
    {
        var store = Create();
        var run = SymbolOf("src/A.cs", "A.Run", arity: 2, line: 5) with { CallSites = [new CallSite(EdgeKind.Calls, "Helper", 1, "this", 7), new CallSite(EdgeKind.Inherits, "Base", 0, "base", 5)] };
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles([FileOf("src/A.cs", run), FileOf("src/B.cs", SymbolOf("src/B.cs", "B.Helper"))], _none), "writing");

            var symbols = Ok(await build.ReadSymbols(_none), "reading symbols");
            Expect.That(symbols.Select(s => s.Ref.Value).Order(StringComparer.Ordinal).SequenceEqual(["fixture:src/A.cs#A.Run", "fixture:src/B.cs#B.Helper"], StringComparer.Ordinal), "every symbol is listed");
            Expect.That(symbols.First(s => s.Name == "Run") is { Kind: SymbolKind.Method, Arity: 2, StartLine: 5, EndLine: 9 } run2 && string.Equals(run2.File, "src/A.cs", StringComparison.Ordinal) && string.Equals(run2.Qualified, "A.Run", StringComparison.Ordinal), "with the fields the resolver needs");

            var sites = new List<CallSiteRow>();
            await foreach (var row in build.ReadCallSites(_none))
            {
                sites.Add(row);
            }

            Expect.That(sites.Count == 2 && sites.TrueForAll(r => r.Caller == run.Ref), "every call site comes with its caller");
            Expect.That(sites.Exists(r => r.Site == new CallSite(EdgeKind.Calls, "Helper", 1, "this", 7)) && sites.Exists(r => r.Site.Kind == EdgeKind.Inherits), "and keeps its name, arity, receiver, line and kind");
        }
    }

    /// <summary>Replacing dependencies and the graph succeeds, and again with new rows.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Dependencies_and_the_graph_can_be_replaced()
    {
        var store = Create();
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            var graph = new GraphSnapshot([new GraphNode("n1", "src/A.cs", "Run", 3, 1), new GraphNode("n2", File: null, Name: null, Line: null, Community: null)], [new GraphEdge("n1", "n2", "calls", "EXTRACTED")]);
            Succeeds(await build.ReplaceDeps([new DepRecord("package.json", "left-pad", "1.0.0", "runtime", "string")], _none), "replacing the dependencies");
            Succeeds(await build.ReplaceDeps([new DepRecord("package.json", "right-pad", "2.0.0", "dev", "")], _none), "replacing them again");
            Succeeds(await build.ReplaceGraph(graph, _none), "replacing the graph");
            Succeeds(await build.ReplaceGraph(new GraphSnapshot([], []), _none), "emptying the graph");
            Succeeds(await build.Publish(Meta(), _none), "publishing");
        }
    }
}
