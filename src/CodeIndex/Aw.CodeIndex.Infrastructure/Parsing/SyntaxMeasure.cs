namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>What one declaration measures to: its normalized shape hash, its cyclomatic complexity and the token list MinHash reads (spec §5.3).</summary>
/// <param name="ShapeHash">16 hex digits of the hash of the node and token kinds, identifiers and literals blanked.</param>
/// <param name="Complexity">1 plus the decision nodes and short-circuit operators, not counting nested local functions and named lambdas (they are symbols of their own); 0 for a type.</param>
/// <param name="Tokens">The normalized tokens of a callable symbol (an identifier is 'id', a literal 'lit', anything else its text); empty for a type.</param>
internal sealed record SyntaxMeasure(string ShapeHash, int Complexity, IReadOnlyList<string> Tokens);
