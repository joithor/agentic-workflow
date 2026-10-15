using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Proposals;

/// <summary>One surface a scoping run found (spec §8.7): it may already be a node of the map, or be new with a kind and its evidence, or name no kind yet.</summary>
/// <param name="Name">What the run calls it (the label to propose).</param>
/// <param name="Key">The node key when the run knows it, else null.</param>
/// <param name="Kind">The proposed kind, or null when the run couldn't tell (then it stays 'unmapped').</param>
/// <param name="Parent">The feature it belongs under, or null.</param>
/// <param name="Evidence">The records it rests on, or null.</param>
/// <param name="Quote">The brief's quote that names it, or null.</param>
public sealed record ProposedSurface(string Name, NodeKey? Key, NodeKind? Kind, NodeKey? Parent, IReadOnlyList<EvidenceRef>? Evidence, string? Quote);
