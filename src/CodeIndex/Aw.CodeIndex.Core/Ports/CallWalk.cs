using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>A reader's callers or callees walk before the facade adds the stamp.</summary>
/// <param name="Edges">The edges by level, at most <see cref="CodeIndexRules.MaxEdgesPerLevel"/> each.</param>
/// <param name="UnresolvedCount">The subject's calls that resolved to nothing.</param>
/// <param name="Truncated">True when a level had more.</param>
/// <param name="Coverage">The whole-walk coverage, from <see cref="CallCoverage.Of"/>.</param>
public sealed record CallWalk(IReadOnlyList<CallEdge> Edges, int UnresolvedCount, bool Truncated, Coverage Coverage);
