using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>The node a lenses view is about.</summary>
/// <param name="Key">Its key.</param>
/// <param name="Label">Its label as the caller reads it (cleaned; fenced for a session).</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
public sealed record LensAnchor(NodeKey Key, string Label, NodeKind Kind, Results.Trust Trust);
