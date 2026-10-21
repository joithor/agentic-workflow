namespace Aw.CodeIndex.Core.Model;

/// <summary>What a parser reports for one symbol in one file (spec §5.3). Free text is raw: the build scrubs and cuts it before it is stored.</summary>
/// <param name="Name">The simple name.</param>
/// <param name="Qualified">The qualified name inside the file's namespace and types.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Arity">The parameter count.</param>
/// <param name="ParameterTypesText">The parameter-type text; its hash separates same-name overloads in a ref.</param>
/// <param name="StartLine">The first line, 1-based.</param>
/// <param name="EndLine">The last line.</param>
/// <param name="Exported">True for 'public' or 'protected' on a visible type.</param>
/// <param name="Signature">The signature text, raw.</param>
/// <param name="ShapeHash">The hash of the normalized shape (node kinds, identifiers and literals blanked).</param>
/// <param name="Complexity">Cyclomatic complexity.</param>
/// <param name="Tokens">The token list MinHash reads; empty when the parser has none.</param>
/// <param name="Body">The symbol's text, raw, or null.</param>
/// <param name="CallSites">The calls and inheritances inside it.</param>
public sealed record ParsedSymbol(
    string Name,
    string Qualified,
    SymbolKind Kind,
    int Arity,
    string ParameterTypesText,
    int StartLine,
    int EndLine,
    bool Exported,
    string Signature,
    string ShapeHash,
    int Complexity,
    IReadOnlyList<string> Tokens,
    string? Body,
    IReadOnlyList<CallSite> CallSites);
