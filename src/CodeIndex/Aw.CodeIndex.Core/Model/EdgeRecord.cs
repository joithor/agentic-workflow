using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A resolved edge to write (the 'edges' table, spec §4.2).</summary>
/// <param name="Source">The calling or inheriting symbol.</param>
/// <param name="Target">The target symbol, or null when it is not in this repo or not resolved.</param>
/// <param name="TargetName">The called name; always kept, so an unresolved call still reads.</param>
/// <param name="Kind">Calls or inherits.</param>
/// <param name="Basis">Syntax or graphify.</param>
/// <param name="Confidence">How sure.</param>
/// <param name="Line">The 1-based line.</param>
/// <param name="Candidates">How many symbols the name matched, kept even when only 8 edges are.</param>
public sealed record EdgeRecord(SymbolRef Source, SymbolRef? Target, string TargetName, EdgeKind Kind, EdgeBasis Basis, Confidence Confidence, int Line, int Candidates);
