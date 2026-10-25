using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A symbol as stored (the 'symbols' table, spec §4.2, with its call sites, bands and body).</summary>
/// <param name="Ref">Its stable reference, unique in the index.</param>
/// <param name="Name">The simple name.</param>
/// <param name="Qualified">The qualified name.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Arity">The parameter count.</param>
/// <param name="StartLine">The first line.</param>
/// <param name="EndLine">The last line.</param>
/// <param name="Exported">Whether it is public API of a visible type.</param>
/// <param name="Utility">Whether its file matches the settings' utility globs.</param>
/// <param name="Signature">Scrubbed and cut at 400 characters.</param>
/// <param name="ShapeHash">The normalized shape hash.</param>
/// <param name="TokenCount">The tokens it has.</param>
/// <param name="Complexity">Cyclomatic complexity.</param>
/// <param name="MinHash">The 64-hash MinHash; empty under 30 tokens (such a symbol has no clone rows).</param>
/// <param name="BandKeys">The 16 LSH band keys of <paramref name="MinHash"/>; empty when it is null.</param>
/// <param name="Body">The scrubbed text kept until embedded, or null.</param>
/// <param name="CallSites">The raw calls and inheritances.</param>
public sealed record SymbolRecord(
    SymbolRef Ref,
    string Name,
    string Qualified,
    SymbolKind Kind,
    int Arity,
    int StartLine,
    int EndLine,
    bool Exported,
    bool Utility,
    string Signature,
    string ShapeHash,
    int TokenCount,
    int Complexity,
    ReadOnlyMemory<byte> MinHash,
    IReadOnlyList<string> BandKeys,
    string? Body,
    IReadOnlyList<CallSite> CallSites);
