using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Testing;

/// <summary>One in-memory index: what <see cref="FakeIndexStore"/> publishes and a build copies.</summary>
internal sealed class FakeIndexData
{
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

    internal SymbolHit? Hit(SymbolRef symbol)
        => Symbols.Where(x => x.Symbol.Ref.Equals(symbol)).Select(x => ToHit(x.File, x.Symbol)).Cast<SymbolHit?>().FirstOrDefault();

    internal static SymbolHit ToHit(FileRecord file, SymbolRecord symbol)
        => new(symbol.Ref, symbol.Kind, symbol.Name, file.Path, symbol.StartLine, symbol.EndLine, symbol.Signature, symbol.Exported);
}
