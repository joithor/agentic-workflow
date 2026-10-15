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
    public static Result<IReadOnlyList<MapOverride>> Plan(ReviewDecision decision, GraphSnapshot draft, IReadOnlyList<MapOverride> overrides, string who, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(overrides);
        var applied = OverrideApplier.Apply(draft, overrides);
        var queue = ReviewQueue.Build(string.Empty, draft, overrides);
        MapOverride Make(OverrideAction action, NodeKey target, string? label = null, NodeKey? other = null, string? rejection = null, LinkId? link = null) =>
            new(action, target, who, at, label, other, rejection, link);

        Result<IReadOnlyList<MapOverride>> One(Result<MapOverride> row) => row.Map<IReadOnlyList<MapOverride>>(r => [r]);
        return decision.Verb switch
        {
            ReviewVerb.AcceptDeterministic => queue.Entries.Where(e => e is { Group: ReviewGroup.Deterministic, Link: null }).Select(e => Make(OverrideAction.Accept, e.Node)).ToList(),
            ReviewVerb.Accept or ReviewVerb.Reject => One(Decide(decision, queue, applied.Graph, Make)),
            ReviewVerb.Undo => One(UndoRules.Check(overrides, decision.Undoes ?? default).Map(o => Make(OverrideAction.Undo, o.Target) with { Undoes = o.ToId() })),
            ReviewVerb.Drop => One(Drop(decision, applied, Make)),
            _ => One(Edit(decision, applied.Graph, Make)),
        };
    }

    private delegate MapOverride Maker(OverrideAction action, NodeKey target, string? label = null, NodeKey? other = null, string? rejection = null, LinkId? link = null);

    private static Result<MapOverride> Decide(ReviewDecision d, ReviewQueueView queue, GraphSnapshot graph, Maker make)
    {
        var reject = d.Verb == ReviewVerb.Reject;
        var action = reject ? OverrideAction.Reject : OverrideAction.Accept;
        if (d.Link is { } link)
        {
            return queue.Entries.FirstOrDefault(e => e.Link?.Value == link.Value) is { } entry
                ? make(action, entry.Node, link: link)
                : Stale($"link {link.Value} is not in the review queue (fix: list it again with 'sindri context map review')");
        }

        var node = d.Node is { } key ? queue.Entries.FirstOrDefault(e => e.Link is null && e.Group != ReviewGroup.Orphaned && e.Node.Value == key.Value) : null;
        return node is null
            ? Stale($"{d.Node?.Value ?? "no node"} is not in the review queue (fix: list it again with 'sindri context map review')")
            : make(action, node.Node, rejection: reject ? RejectionIdentity.For(graph.Resolve(node.Node)!) : null);   // why: queue entries come from this graph's nodes
    }

    private static Result<MapOverride> Drop(ReviewDecision d, AppliedMap applied, Maker make)
    {
        var orphan = d.Undoes is { } id ? applied.Orphaned.FirstOrDefault(o => o.ToId().Value == id.Value) : null;
        return orphan is null
            ? Stale($"{d.Undoes?.Value ?? "no override"} is not an orphaned override (fix: list the queue again)")
            : make(OverrideAction.Undo, orphan.Target) with { Undoes = orphan.ToId() };
    }

    private static Result<MapOverride> Edit(ReviewDecision d, GraphSnapshot graph, Maker make)
    {
        if (d.Node is not { } key)
        {
            return ContextErrors.QueryInvalid.With($"{d.Verb.ToString().ToLowerInvariant()} names a node (fix: pass --node)");
        }

        var node = graph.Resolve(key);
        var other = d.Other is { } o ? graph.Resolve(o) : null;
        var action = d.Verb switch
        {
            ReviewVerb.Rename => OverrideAction.Rename,
            ReviewVerb.Move => OverrideAction.Move,
            ReviewVerb.Merge => OverrideAction.Merge,
            ReviewVerb.Add => OverrideAction.Add,
            _ => OverrideAction.Delete,
        };
        return (d.Verb, node, other) switch
        {
            (ReviewVerb.Add, null, _) when d.Other is null || other is not null => d.Label is { Length: > 0 } && NodeKinds.HasValidSurfaceKind(key) && key.Kind != NodeKind.Decision
                ? make(action, key, d.Label, other?.Key)
                : ContextErrors.QueryInvalid.With("add needs a label and a feature, surface, action, thing, flow, record or code key"),
            (ReviewVerb.Add, _, _) => Stale($"{key.Value} exists already, or its parent doesn't (fix: list the queue again)"),
            (_, null, _) => Stale($"{key.Value} is not in the draft (fix: list the queue again)"),
            (ReviewVerb.Rename, { } n, _) => string.IsNullOrWhiteSpace(d.Label) ? ContextErrors.QueryInvalid.With("rename needs a label") : make(action, n.Key, d.Label),
            (ReviewVerb.Move, { } n, { } p) when EdgeRules.Allows(EdgeType.PartOf, n.Key.Kind, p.Key.Kind) && p.Key.Value != n.Key.Value => make(action, n.Key, other: p.Key),
            (ReviewVerb.Merge, { } n, { } p) when n.Key.Kind == p.Key.Kind && p.Key.Value != n.Key.Value => make(action, n.Key, other: p.Key),
            (ReviewVerb.Delete, { } n, _) => make(action, n.Key),
            _ => Stale($"{d.Verb.ToString().ToLowerInvariant()} needs another node that exists and fits (fix: pass --under or --into with a node of the draft)"),
        };
    }

    private static Error Stale(string message) => ContextErrors.ReviewTargetStale.With(message);
}
