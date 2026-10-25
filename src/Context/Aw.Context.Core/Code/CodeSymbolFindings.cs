using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>What the code index gave the code lens (S-9a): the 'code-index' row of 'searched.sources' and the callers and callees.</summary>
/// <param name="Row">The row.</param>
/// <param name="Callers">The callers of the symbols the listed files define.</param>
/// <param name="Callees">Their callees.</param>
public sealed record CodeSymbolFindings(SearchedSource Row, CodeSymbols Callers, CodeSymbols Callees);
