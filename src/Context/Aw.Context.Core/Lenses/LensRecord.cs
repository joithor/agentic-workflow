using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>A record (a ticket, a doc, a thread) that mentions scope nodes. Its text is never fetched here.</summary>
/// <param name="Key">The record's node key.</param>
/// <param name="Label">Its label as the caller reads it.</param>
/// <param name="Source">Where it lives: 'linear', 'linear:doc', 'slack', 'onyx' or 'repo-doc'.</param>
/// <param name="Mentions">The scope nodes it mentions.</param>
/// <param name="Trust">Record text is untrusted.</param>
/// <param name="State">The citation's state; 'unchecked' until a source is read.</param>
/// <param name="Cites">The citation markers (empty in S-4).</param>
public sealed record LensRecord(NodeKey Key, string Label, string Source, IReadOnlyList<NodeKey> Mentions, Results.Trust Trust, CitationState State, IReadOnlyList<string> Cites);
