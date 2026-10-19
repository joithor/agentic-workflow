using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>Turns one review decision into override rows (S-4): checks it still applies to the latest draft (SND-CONTEXT-047) and writes nothing itself. Pure.</summary>
public static class ReviewPlanner
{
    /// <summary>Plans the rows for a decision.</summary>
    /// <param name="decision">What the person decided.</param>
    /// <param name="draft">The latest draft as built (before overrides).</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="who">The OS user who decided.</param>
    /// <param name="at">When.</param>
    /// <returns>The rows to append, or SND-CONTEXT-047 when the target is gone or decided already.</returns>
    public static Result<IReadOnlyList<MapOverride>> Plan(ReviewDecision decision, GraphSnapshot draft, IReadOnlyList<MapOverride> overrides, string who, DateTimeOffset at) =>
        Plan(decision, new ReviewBasis(draft, overrides), who, at);

    /// <summary>Plans the rows for a decision against a basis (the latest draft, or the current map when a link or an undo is decided with no draft).</summary>
    /// <param name="decision">What the person decided.</param>
    /// <param name="basis">The map as the caller may see it, before overrides.</param>
    /// <param name="who">The OS user who decided.</param>
    /// <param name="at">When.</param>
    /// <returns>The rows to append, or SND-CONTEXT-047 when the target is gone or decided already.</returns>
    public static Result<IReadOnlyList<MapOverride>> Plan(ReviewDecision decision, ReviewBasis basis, string who, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(basis);
        var (draft, overrides, onDraft) = basis;
        var applied = OverrideApplier.Apply(draft, overrides);
        var world = new World(who, at, overrides, applied, ReviewBacklog.Build(string.Empty, draft, overrides), draft, onDraft);
        return decision.Verb switch
        {
            ReviewVerb.AcceptDeterministic => world.AcceptDeterministic(),
            ReviewVerb.Accept or ReviewVerb.Reject => One(decision.Link is { } link ? world.DecideLink(decision, link) : world.DecideNode(decision)),
            ReviewVerb.Undo => decision.UndoesAll is null ? One(world.Undo(decision)) : world.UndoAll(decision.UndoesAll),
            ReviewVerb.Drop => One(world.Drop(decision)),
            ReviewVerb.Repoint => world.Repoint(decision),
            ReviewVerb.Rename or ReviewVerb.Move or ReviewVerb.Merge or ReviewVerb.Add or ReviewVerb.Delete => One(world.Edit(decision)),
            _ => One(Stale($"{decision.Verb.ToString().ToLowerInvariant()} is not a review action")),
        };
    }

    /// <summary>Plans a whole review file's decisions, each on top of the rows planned before it, against one build of the map.</summary>
    /// <param name="decisions">The decisions, in file order.</param>
    /// <param name="basis">The map as the caller may see it, before overrides.</param>
    /// <param name="who">The OS user who decided.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Stops the plan between decisions.</param>
    /// <returns>The rows to append and the skipped lines (one per decision that is stale).</returns>
    public static (IReadOnlyList<MapOverride> Rows, IReadOnlyList<string> Skipped) PlanAll(IReadOnlyList<ReviewDecision> decisions, ReviewBasis basis, string who, DateTimeOffset at, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private static Result<IReadOnlyList<MapOverride>> One(Result<MapOverride> row) => row.Map<IReadOnlyList<MapOverride>>(r => [r]);

    private static Error Stale(string message) => ContextErrors.ReviewTargetStale.With(message);

    private sealed record World(string Who, DateTimeOffset At, IReadOnlyList<MapOverride> Overrides, AppliedMap Applied, ReviewQueueView Queue, GraphSnapshot Draft, bool OnDraft)
    {
        private GraphSnapshot Graph => Applied.Graph;

        private string Basis => OnDraft ? "the draft" : "the current map";

        private string OrphanFix => OnDraft ? "list the queue again" : "find the orphan's id with 'sindri context map review --history'";

        internal Result<IReadOnlyList<MapOverride>> AcceptDeterministic()
        {
            IReadOnlyList<MapOverride> rows = [.. Queue.Entries.Where(e => e is { Group: ReviewGroup.Deterministic, Link: null }).Select(e => Row(OverrideAction.Accept, e.Node))];
            return Kernel.Results.Success(rows);
        }

        internal Result<MapOverride> DecideLink(ReviewDecision d, LinkId link)
        {
            var entry = Queue.Entries.FirstOrDefault(e => e.Link?.Value == link.Value);
            return entry is null
                ? Stale($"link {link.Value} is not in {(OnDraft ? "the review queue" : "the current map")} (fix: {(OnDraft ? "list it again with 'sindri context map review'" : "find its id with 'sindri context filemap'")})")
                : Row(Verb(d), entry.Node) with { Link = link };
        }

        internal Result<MapOverride> DecideNode(ReviewDecision d)
        {
            var entry = d.Node is { } key ? Queue.Entries.FirstOrDefault(e => e.Link is null && e.Group != ReviewGroup.Orphaned && e.Node.Value == key.Value) : null;
            if (entry is null)
            {
                return Stale($"{d.Node?.Value ?? "no node"} is not in the review queue (fix: list it again with 'sindri context map review')");
            }

            var row = Row(Verb(d), entry.Node);
            return d.Verb == ReviewVerb.Reject && Graph.Resolve(entry.Node) is { } node ? row with { Rejection = RejectionIdentity.For(node) } : row;
        }

        internal Result<MapOverride> Undo(ReviewDecision d) =>
            d.Undoes is not { } id
                ? Stale("undo names an override id (fix: list the history with 'sindri context map review --history')")
                : UndoRules.Check(Overrides, id).Map(o => Row(OverrideAction.Undo, o.Target) with { Undoes = o.ToId() });

        /// <summary>Undoes every named override, or none: the first id that is unknown or undone already refuses the whole call and is named.</summary>
        internal Result<IReadOnlyList<MapOverride>> UndoAll(IReadOnlyList<OverrideId> ids)
        {
            var distinct = ids.DistinctBy(i => i.Value, StringComparer.Ordinal).ToList();
            if (distinct.Count == 0)
            {
                return Stale("undo names no override (fix: list the history with 'sindri context map review --history')");
            }

            var rows = new List<MapOverride>();
            foreach (var id in distinct)
            {
                var found = UndoRules.Check(Overrides, id);
                if (found.IsError)
                {
                    return found.Error;
                }

                rows.Add(Row(OverrideAction.Undo, found.Value.Target) with { Undoes = found.Value.ToId() });
            }

            return rows;
        }

        /// <summary>Points an orphaned node decision at a node of the draft: an undo of the orphan, then the same decision on the new node.</summary>
        internal Result<IReadOnlyList<MapOverride>> Repoint(ReviewDecision d)
        {
            var orphan = d.Undoes is { } id ? Applied.Orphaned.FirstOrDefault(o => o.ToId().Value == id.Value) : null;
            if (orphan is null || orphan.Link is not null)
            {
                return Stale($"{d.Undoes?.Value ?? "no override"} is not an orphaned decision about a node (fix: {OrphanFix})");
            }

            var node = d.Node is { } key ? Graph.Resolve(key) : null;
            if (node is null)
            {
                return Stale($"{d.Node?.Value ?? "no node"} is not in {Basis} (fix: pass --to with a node of {Basis})");
            }

            return new List<MapOverride>
            {
                Row(OverrideAction.Undo, orphan.Target) with { Undoes = orphan.ToId() },
                orphan with { Target = node.Key, Who = Who, At = At, Rejection = null },
            };
        }

        internal Result<MapOverride> Drop(ReviewDecision d)
        {
            var orphan = d.Undoes is { } id ? Applied.Orphaned.FirstOrDefault(o => o.ToId().Value == id.Value) : null;
            return orphan is null
                ? Stale($"{d.Undoes?.Value ?? "no override"} is not an orphaned override (fix: {OrphanFix})")
                : Row(OverrideAction.Undo, orphan.Target) with { Undoes = orphan.ToId() };
        }

        internal Result<MapOverride> Edit(ReviewDecision d)
        {
            if (d.Node is not { } key)
            {
                return ContextErrors.QueryInvalid.With($"{d.Verb.ToString().ToLowerInvariant()} names a node (fix: pass --node)");
            }

            var node = Graph.Resolve(key);
            var other = d.Other is { } o ? Graph.Resolve(o) : null;
            if (d.Verb == ReviewVerb.Add)
            {
                return Add(d, key, node, other);
            }

            if (node is null)
            {
                return Stale($"{key.Value} is not in the draft (fix: list the queue again)");
            }

            return d.Verb switch
            {
                ReviewVerb.Rename => string.IsNullOrWhiteSpace(d.Label) ? ContextErrors.QueryInvalid.With("rename needs a label") : Row(OverrideAction.Rename, node.Key, d.Label),
                ReviewVerb.Move when other is not null && EdgeRules.Allows(EdgeType.PartOf, node.Key.Kind, other.Key.Kind) && other.Key.Value != node.Key.Value => Row(OverrideAction.Move, node.Key, other: other.Key),
                ReviewVerb.Merge when other is not null && node.Key.Kind == other.Key.Kind && other.Key.Value != node.Key.Value => Row(OverrideAction.Merge, node.Key, other: other.Key),
                ReviewVerb.Delete => Row(OverrideAction.Delete, node.Key),
                ReviewVerb.Accept or ReviewVerb.Reject or ReviewVerb.AcceptDeterministic or ReviewVerb.Undo or ReviewVerb.Drop or ReviewVerb.Repoint or ReviewVerb.Add or ReviewVerb.Move or ReviewVerb.Merge =>
                    Stale($"{d.Verb.ToString().ToLowerInvariant()} needs another node that exists and fits (fix: pass --under or --into with a node of the draft)"),
                _ => Stale($"{d.Verb.ToString().ToLowerInvariant()} is not an edit"),
            };
        }

        private Result<MapOverride> Add(ReviewDecision d, NodeKey key, GraphNode? existing, GraphNode? parent)
        {
            if (existing is not null || (d.Other is not null && parent is null))
            {
                return Stale($"{key.Value} exists already, or its parent doesn't (fix: list the queue again)");
            }

            return !string.IsNullOrWhiteSpace(d.Label) && NodeKinds.HasValidSurfaceKind(key) && key.Kind != NodeKind.Decision
                ? Row(OverrideAction.Add, key, d.Label, parent?.Key)
                : ContextErrors.QueryInvalid.With("add needs a label and a feature, surface, action, thing, flow, record or code key");
        }

        private MapOverride Row(OverrideAction action, NodeKey target, string? label = null, NodeKey? other = null) => new(action, target, Who, At, label, other);

        private static OverrideAction Verb(ReviewDecision d) => d.Verb == ReviewVerb.Reject ? OverrideAction.Reject : OverrideAction.Accept;
    }
}
