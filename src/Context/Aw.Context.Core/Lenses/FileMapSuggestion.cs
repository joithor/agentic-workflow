using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>A suggested file link a person can accept or reject.</summary>
/// <param name="Link">The link's id.</param>
/// <param name="File">The file's 'code:' key.</param>
/// <param name="Feature">The node it is suggested to implement.</param>
/// <param name="Confidence">0 to 1.</param>
public sealed record FileMapSuggestion(LinkId Link, NodeKey File, NodeKey Feature, double Confidence);
