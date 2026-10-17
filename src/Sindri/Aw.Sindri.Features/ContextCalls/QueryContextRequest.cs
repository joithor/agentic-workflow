using Aw.Context.Core.Results;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>A context question (GET /sindri/context/query, 'sindri context query'; context spec §8.5).</summary>
/// <param name="Q">Free text, or null.</param>
/// <param name="Node">A graph node anchor, or null.</param>
/// <param name="Item">An item anchor, or null.</param>
/// <param name="Symbol">A file or symbol anchor, or null.</param>
/// <param name="Depth">The cascade depth as sent (a whole number 0 to 4; anything else is SND-CONTEXT-006), or null.</param>
/// <param name="Budget">The token budget as sent (a whole number of 1 or more; anything else is SND-CONTEXT-006), or null.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="Diff">Two commit shas, 'base..head' (the diff shape), or null.</param>
/// <param name="Repo">The repo the diff is read in, or null (the only repo in the caller's scope).</param>
/// <param name="MapVersion">A retained map version to read the graph as of, or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record QueryContextRequest(string? Q, string? Node = null, string? Item = null, string? Symbol = null, string? Depth = null,
    string? Budget = null, bool Draft = false, string? Diff = null, string? Repo = null, string? MapVersion = null) : IQuery<ContextResult>;
