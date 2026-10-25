using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Code;

/// <summary>A migration or schema file that names a data thing.</summary>
/// <param name="Path">The path in the repo.</param>
/// <param name="Label">The path.</param>
/// <param name="Kind">'code'.</param>
/// <param name="Thing">The thing it names.</param>
/// <param name="Matched">The form of the thing's name that matched.</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
/// <param name="Anchors">The anchors whose own scope touches the thing.</param>
public sealed record CodeDataFile(string Path, string Label, NodeKind Kind, NodeKey Thing, string Matched, Results.Trust Trust, IReadOnlyList<NodeKey> Anchors);
