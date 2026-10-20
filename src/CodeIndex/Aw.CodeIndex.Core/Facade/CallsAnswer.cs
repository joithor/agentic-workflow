using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>The answer to Callers or Callees.</summary>
/// <param name="Stamp">Which index answered; its layer is the graph.</param>
/// <param name="Subject">The symbol asked about.</param>
/// <param name="Edges">At most <see cref="CodeIndexRules.MaxEdgesPerLevel"/> per level.</param>
/// <param name="UnresolvedCount">Calls of the symbol that resolved to nothing.</param>
/// <param name="Truncated">True when a level had more than were returned.</param>
public sealed record CallsAnswer(AnswerStamp Stamp, SymbolRef Subject, IReadOnlyList<CallEdge> Edges, int UnresolvedCount, bool Truncated);
