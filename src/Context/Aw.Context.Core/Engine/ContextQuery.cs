namespace Aw.Context.Core.Engine;

/// <summary>
/// A question (spec §8.1). Exactly one shape: free <see cref="Text"/>, or a <see cref="Node"/>, <see cref="Item"/> or
/// <see cref="Symbol"/> anchor (the graph shapes arrive in later phases and answer SND-CONTEXT-003 until then).
/// </summary>
/// <param name="Text">Free text, or null.</param>
/// <param name="Node">A graph node key, name or alias, or null.</param>
/// <param name="Item">A Sindri item id or tracker key, or null.</param>
/// <param name="Symbol">A file path or symbol, or null.</param>
/// <param name="Depth">The cascade depth (at most 4), or null for the profile's.</param>
/// <param name="Budget">The token budget, or null for the default; capped at 'context.maxBudget'.</param>
/// <param name="Draft">True to read the draft map instead of the current one.</param>
public sealed record ContextQuery(string? Text, string? Node = null, string? Item = null, string? Symbol = null, int? Depth = null, int? Budget = null, bool Draft = false);
