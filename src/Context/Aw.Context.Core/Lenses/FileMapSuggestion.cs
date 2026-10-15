using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>A suggested file link a person can accept or reject.</summary>
/// <param name="LinkId">The link's id.</param>
/// <param name="File">The file's path.</param>
/// <param name="Feature">The node it is suggested to implement.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Evidence">The evidence keys behind it.</param>
public sealed record FileMapSuggestion(LinkId LinkId, string File, NodeKey Feature, double Confidence, IReadOnlyList<string> Evidence);
