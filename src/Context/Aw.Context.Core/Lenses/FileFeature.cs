using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>A feature (or other node) a file or directory is linked to.</summary>
/// <param name="Key">The node.</param>
/// <param name="Provenance">Of the link.</param>
/// <param name="Confidence">Of a model link; null for the others.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind the link.</param>
/// <param name="LinkId">The link, to accept or reject it; set on a file's unconfirmed link.</param>
/// <param name="Edge">'implements' or 'tests'.</param>
public sealed record FileFeature(NodeKey Key, Provenance Provenance, double? Confidence, bool Confirmed, LinkId? LinkId, EdgeType Edge);
