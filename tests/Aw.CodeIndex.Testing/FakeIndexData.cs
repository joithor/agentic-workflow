using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Testing;

/// <summary>One in-memory index: what <see cref="FakeIndexStore"/> publishes and a build copies.</summary>
internal sealed class FakeIndexData
{
    internal static readonly HashSet<SymbolKind> ClassLike = [SymbolKind.Class, SymbolKind.Record, SymbolKind.Struct, SymbolKind.Interface, SymbolKind.Enum];

    internal IndexHeader? Header { get; set; }

    internal Dictionary<string, FileRecord> Files { get; } = new(StringComparer.Ordinal);

    internal List<EdgeRecord> Edges { get; } = [];

    internal Dictionary<LayerName, LayerRecord> Layers { get; } = [];

    internal List<DepRecord> Deps { get; } = [];

    internal GraphSnapshot Graph { get; set; } = new([], []);

    internal Dictionary<SymbolRef, EmbeddingRecord> Embeddings { get; } = [];

    internal IEnumerable<(FileRecord File, SymbolRecord Symbol)> Symbols
        => Files.Values.SelectMany(f => f.Symbols.Select(s => (f, s)));

    internal FakeIndexData Copy()
    {
        var copy = new FakeIndexData { Header = Header, Graph = Graph };
        foreach (var (path, file) in Files)
        {
            copy.Files[path] = file;
        }

        copy.Edges.AddRange(Edges);
        foreach (var (layer, row) in Layers)
        {
            copy.Layers[layer] = row;
        }

        copy.Deps.AddRange(Deps);
        foreach (var (symbol, vector) in Embeddings)
        {
            copy.Embeddings[symbol] = vector;
        }

        return copy;
    }

    /// <summary>The store's unique index on edges: a repeat of source, name, kind, basis, line and target is one edge.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>Its key.</returns>
    internal static (SymbolRef, string, EdgeKind, EdgeBasis, int, SymbolRef?) Key(EdgeRecord edge)
        => (edge.Source, edge.TargetName, edge.Kind, edge.Basis, edge.Line, edge.Target);

    /// <summary>Takes the files' symbols out the way the SQLite store does: edges out of them go, edges into them lose their target (a repeat of an unresolved edge is dropped), and their vectors go.</summary>
    /// <param name="paths">The paths about to be rewritten or removed.</param>
    internal void Detach(IEnumerable<string> paths)
    {
        var gone = paths.Where(Files.ContainsKey).SelectMany(p => Files[p].Symbols.Select(s => s.Ref)).ToHashSet();
        if (gone.Count == 0)
        {
            return;
        }

        _ = Edges.RemoveAll(e => gone.Contains(e.Source));
        for (var i = 0; i < Edges.Count; i++)
        {
            if (Edges[i].Target is { } target && gone.Contains(target))
            {
                Edges[i] = Edges[i] with { Target = null };
            }
        }

        var seen = new HashSet<(SymbolRef, string, EdgeKind, EdgeBasis, int, SymbolRef?)>();
        _ = Edges.RemoveAll(e => !seen.Add(Key(e)));
        foreach (var symbol in gone)
        {
            _ = Embeddings.Remove(symbol);
        }
    }

    /// <summary>The members, of the asked arity, that carry the subject's name in the types that inherit (at any depth) from the subject's own type.</summary>
    /// <param name="subject">The member.</param>
    /// <returns>The implementing members, not including the subject.</returns>
    internal List<SymbolRef> ImplementationsOf(SymbolRecord subject)
    {
        var suffix = "." + subject.Name;
        var container = subject.Qualified.EndsWith(suffix, StringComparison.Ordinal) ? subject.Qualified[..^suffix.Length] : string.Empty;
        var bases = Symbols.Where(x => ClassLike.Contains(x.Symbol.Kind) && string.Equals(x.Symbol.Qualified, container, StringComparison.Ordinal)).Select(x => x.Symbol.Ref).ToHashSet();
        var implementors = new HashSet<SymbolRef>();
        for (var found = true; found;)
        {
            var next = Edges.Where(e => e.Kind == EdgeKind.Inherits && e.Target is { } t && (bases.Contains(t) || implementors.Contains(t))).Select(e => e.Source).ToList();
            found = next.Where(implementors.Add).ToList().Count > 0;
        }

        var typeNames = Symbols.Where(x => implementors.Contains(x.Symbol.Ref)).Select(x => x.Symbol.Qualified).ToHashSet(StringComparer.Ordinal);
        return [.. Symbols
            .Where(x => !x.Symbol.Ref.Equals(subject.Ref) && !ClassLike.Contains(x.Symbol.Kind) && x.Symbol.Arity == subject.Arity && typeNames.Any(t => string.Equals(x.Symbol.Qualified, t + suffix, StringComparison.Ordinal)))
            .Select(x => x.Symbol.Ref)];
    }

    internal SymbolHit? Hit(SymbolRef symbol)
        => Symbols.Where(x => x.Symbol.Ref.Equals(symbol)).Select(x => ToHit(x.File, x.Symbol)).Cast<SymbolHit?>().FirstOrDefault();

    internal static SymbolHit ToHit(FileRecord file, SymbolRecord symbol)
        => new(symbol.Ref, symbol.Kind, symbol.Name, file.Path, symbol.StartLine, symbol.EndLine, symbol.Signature, symbol.Exported);
}
