using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A symbol as the resolver needs it: enough to match a name and rank closeness (same file, namespace, project directory, path order), nothing else.</summary>
/// <param name="Ref">The symbol's ref.</param>
/// <param name="File">Its file's path.</param>
/// <param name="Name">The simple name.</param>
/// <param name="Qualified">The qualified name.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Arity">The parameter count.</param>
/// <param name="StartLine">The first line (graph nodes are matched to the enclosing span).</param>
/// <param name="EndLine">The last line.</param>
public sealed record ResolutionSymbol(SymbolRef Ref, string File, string Name, string Qualified, SymbolKind Kind, int Arity, int StartLine, int EndLine);
