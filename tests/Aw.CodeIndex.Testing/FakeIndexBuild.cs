using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>A build over a private copy of the fake index; <see cref="Publish"/> swaps it in, disposing without it discards it.</summary>
internal sealed class FakeIndexBuild(FakeIndexData data, IndexHeader? previous, bool incremental, Action<FakeIndexData> publish) : IIndexBuild
{
    private static readonly HashSet<SymbolKind> _classLike = [SymbolKind.Class, SymbolKind.Record, SymbolKind.Struct, SymbolKind.Interface, SymbolKind.Enum];

    public bool Incremental { get; } = incremental;

    public IndexHeader? Previous { get; } = previous;

    public ValueTask<Result<IReadOnlyList<StoredFile>>> StoredFiles(CancellationToken cancellationToken)
        => Ok<IReadOnlyList<StoredFile>>([.. data.Files.Values.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => new StoredFile(f.Path, f.Blob, f.Parsed))]);

    public ValueTask<Result<Unit>> WriteFiles(IReadOnlyList<FileRecord> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            data.Files[file.Path] = file;
        }

        PruneEdges();
        return Done();
    }

    public ValueTask<Result<Unit>> RemoveFiles(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        foreach (var path in paths)
        {
            _ = data.Files.Remove(path);
        }

        PruneEdges();
        return Done();
    }

    public ValueTask<Result<Unit>> ReplaceDeps(IReadOnlyList<DepRecord> deps, CancellationToken cancellationToken)
    {
        data.Deps.Clear();
        data.Deps.AddRange(deps);
        return Done();
    }

    public ValueTask<Result<IReadOnlyList<ResolutionSymbol>>> ReadSymbols(CancellationToken cancellationToken)
        => Ok<IReadOnlyList<ResolutionSymbol>>([.. data.Symbols.Select(x => new ResolutionSymbol(x.Symbol.Ref, x.File.Path, x.Symbol.Name, x.Symbol.Qualified, x.Symbol.Kind, x.Symbol.Arity, x.Symbol.StartLine, x.Symbol.EndLine))]);

    public async IAsyncEnumerable<CallSiteRow> ReadCallSites([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var (_, symbol) in data.Symbols.ToList())
        {
            foreach (var site in symbol.CallSites)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new CallSiteRow(symbol.Ref, site);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public ValueTask<Result<Unit>> ClearEdges(EdgeBasis basis, CancellationToken cancellationToken)
    {
        _ = data.Edges.RemoveAll(e => e.Basis == basis);
        return Done();
    }

    public ValueTask<Result<Unit>> AddEdges(IReadOnlyList<EdgeRecord> edges, CancellationToken cancellationToken)
    {
        foreach (var edge in edges.Where(e => !data.Edges.Contains(e)))
        {
            data.Edges.Add(edge);
        }

        return Done();
    }

    public ValueTask<Result<Unit>> ReplaceGraph(GraphSnapshot graph, CancellationToken cancellationToken)
    {
        data.Graph = graph;
        return Done();
    }

    public ValueTask<Result<IReadOnlyList<EmbedCandidate>>> EmbedCandidates(int limit, CancellationToken cancellationToken)
        => Ok<IReadOnlyList<EmbedCandidate>>([.. data.Symbols
            .Where(x => x.Symbol.Body is not null && !_classLike.Contains(x.Symbol.Kind) && !data.Embeddings.ContainsKey(x.Symbol.Ref))
            .OrderBy(x => x.Symbol.Ref.Value, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new EmbedCandidate(x.Symbol.Ref, x.Symbol.Name, x.Symbol.Signature, x.Symbol.Body!))]);

    public ValueTask<Result<Unit>> WriteEmbeddings(IReadOnlyList<EmbeddingRecord> vectors, CancellationToken cancellationToken)
    {
        foreach (var vector in vectors)
        {
            data.Embeddings[vector.Symbol] = vector;
        }

        var embedded = vectors.Select(v => v.Symbol).ToHashSet();
        foreach (var file in data.Files.Values.ToList())
        {
            if (file.Symbols.Any(s => embedded.Contains(s.Ref)))
            {
                data.Files[file.Path] = file with { Symbols = [.. file.Symbols.Select(s => embedded.Contains(s.Ref) ? s with { Body = null } : s)] };
            }
        }

        return Done();
    }

    public ValueTask<Result<EmbeddingProgress>> CountEmbeddings(CancellationToken cancellationToken)
        => Ok(new EmbeddingProgress(data.Embeddings.Count, data.Symbols.Count(x => !_classLike.Contains(x.Symbol.Kind))));

    public ValueTask<Result<Unit>> SetLayer(LayerRecord layer, CancellationToken cancellationToken)
    {
        data.Layers[layer.Layer] = layer;
        return Done();
    }

    public ValueTask<Result<IndexHeader>> Publish(IndexMeta meta, CancellationToken cancellationToken)
    {
        var counts = new IndexCounts(data.Files.Count, data.Symbols.Count(), data.Edges.Count);
        data.Header = new IndexHeader(meta with { Counts = counts, Schema = CodeIndexRules.SchemaVersion }, [.. data.Layers.Values.OrderBy(l => l.Layer)]);
        publish(data);
        return Ok(data.Header);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // A file's symbols cascade away with it, and an edge to a vanished symbol loses its target.
    private void PruneEdges()
    {
        var live = data.Symbols.Select(x => x.Symbol.Ref).ToHashSet();
        _ = data.Edges.RemoveAll(e => !live.Contains(e.Source));
        for (var i = 0; i < data.Edges.Count; i++)
        {
            if (data.Edges[i].Target is { } target && !live.Contains(target))
            {
                data.Edges[i] = data.Edges[i] with { Target = null };
            }
        }
    }

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private static ValueTask<Result<Unit>> Done() => ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
