using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Review;

/// <summary>One review decision (a call of 'map review' or POST map/review). Which fields a verb reads is in <see cref="ReviewVerb"/>'s members; the rest are null.</summary>
/// <param name="Verb">What to do.</param>
/// <param name="Node">The node it applies to (accept, reject, rename, move, merge, add, delete, drop), or null.</param>
/// <param name="Link">The link it applies to (accept or reject of a suggested link; then <paramref name="Node"/> is null), or null.</param>
/// <param name="Undoes">The override to reverse (undo), or null.</param>
/// <param name="Label">The new label (rename, add), or null.</param>
/// <param name="Other">The node to merge into or move under (merge, move; for add its parent), or null.</param>
/// <param name="UndoesAll">The overrides to reverse together (undo with a list: all or none), or null; not together with <paramref name="Undoes"/>.</param>
public sealed record ReviewDecision(
    ReviewVerb Verb,
    NodeKey? Node = null,
    LinkId? Link = null,
    OverrideId? Undoes = null,
    string? Label = null,
    NodeKey? Other = null,
    IReadOnlyList<OverrideId>? UndoesAll = null);
