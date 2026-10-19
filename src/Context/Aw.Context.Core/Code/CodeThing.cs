using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Code;

/// <summary>A data thing the own scope reads or writes.</summary>
/// <param name="Key">Its key.</param>
/// <param name="Label">Its label as the caller reads it.</param>
/// <param name="Kind">'thing'.</param>
/// <param name="Access">How the own scope touches it.</param>
/// <param name="Via">The own-scope surfaces and actions that read or write it.</param>
/// <param name="Searchable">False when no form of its names is long enough to search for.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind it.</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
/// <param name="Anchors">The anchors whose own scope touches it.</param>
public sealed record CodeThing(
    NodeKey Key,
    string Label,
    NodeKind Kind,
    DataAccess Access,
    IReadOnlyList<NodeKey> Via,
    bool Searchable,
    Provenance Provenance,
    double Confidence,
    bool Confirmed,
    Results.Trust Trust,
    IReadOnlyList<NodeKey> Anchors);
