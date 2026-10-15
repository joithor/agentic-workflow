using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>One node of the own scope.</summary>
/// <param name="Node">The key.</param>
/// <param name="Label">The label as the caller reads it.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind it, or an accepted override (the Confirmation rule).</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
public sealed record LensItem(NodeKey Node, string Label, NodeKind Kind, Provenance Provenance, double Confidence, bool Confirmed, Results.Trust Trust);
