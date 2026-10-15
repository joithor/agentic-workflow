using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>One node in a lens band.</summary>
/// <param name="Key">The key.</param>
/// <param name="Label">The label, scrubbed.</param>
/// <param name="Relation">How it relates to the anchor.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind it, or an accepted override (the Confirmation rule).</param>
/// <param name="Link">For a code item: the link to the anchor's scope that put it here, or null.</param>
public sealed record LensItem(NodeKey Key, string Label, LensRelation Relation, Provenance Provenance, double Confidence, bool Confirmed, LinkId? Link);
