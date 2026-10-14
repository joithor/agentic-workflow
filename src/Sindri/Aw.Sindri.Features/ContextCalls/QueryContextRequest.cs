using Aw.Context.Core.Results;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>A context question (GET /sindri/context/query, 'sindri context query'; context spec §8.5).</summary>
/// <param name="Q">Free text, or null.</param>
/// <param name="Node">A graph node anchor, or null.</param>
/// <param name="Item">An item anchor, or null.</param>
/// <param name="Symbol">A file or symbol anchor, or null.</param>
/// <param name="Depth">The cascade depth, or null.</param>
/// <param name="Budget">The token budget, or null.</param>
/// <param name="Draft">True to read the draft map.</param>
public sealed record QueryContextRequest(string? Q, string? Node = null, string? Item = null, string? Symbol = null, int? Depth = null,
    int? Budget = null, bool Draft = false) : IQuery<ContextResult>;
