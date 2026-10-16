using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Linking;

/// <summary>What the engine kept of a model's extraction answer, and how many parts of it it refused (SND-CONTEXT-057).</summary>
/// <param name="Decisions">The decisions whose quote is verbatim in the record text.</param>
/// <param name="Edges">Their decides edges (to nodes the record links) and supersedes edges.</param>
/// <param name="Refused">Parts refused: a quote not found, a target the record doesn't link, a malformed answer.</param>
public sealed record ExtractionOutcome(IReadOnlyList<DecisionRow> Decisions, IReadOnlyList<GraphEdge> Edges, int Refused);
