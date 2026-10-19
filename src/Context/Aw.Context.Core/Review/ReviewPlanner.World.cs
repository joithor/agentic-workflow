using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Review;

public static partial class ReviewPlanner
{
    // The map a decision is planned against, with the queue indexed by id so a decision is a lookup, not a scan.
    private sealed record World(string Who, DateTimeOffset At, IReadOnlyList<MapOverride> Overrides, AppliedMap Applied, ReviewQueueView Queue, GraphSnapshot Draft, bool OnDraft)
    {
        private readonly Dictionary<string, ReviewEntry> _nodes = Index(Queue.Entries.Where(e => e.Link is null && e.Group != ReviewGroup.Orphaned), e => e.Node.Value);
        private readonly Dictionary<string, List<ReviewEntry>> _links = Group(Queue.Entries, e => e.Link is { } link ? [link.Value] : []);
        private readonly Dictionary<string, List<ReviewEntry>> _linksAt = Group(Queue.Entries.Where(e => e.Link is not null && e.Group != ReviewGroup.Orphaned), e => [e.Node.Value, .. e.Parent is { } to ? [to.Value] : Array.Empty<string>()]);
        private readonly HashSet<ReviewEntry> _claimed = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<string> _decidedLinks = new(StringComparer.Ordinal);
        private Dictionary<string, GraphNode>? _graphNodes;
        private Dictionary<string, NodeKey>? _aliases;

        /// <summary>
        /// False once a planned row may change the queue in a way a claim doesn't capture, so the next decision needs a fresh build: on a
        /// map whose keys aren't plain (a node key held twice, an alias key that is also a node's, one link id twice), after every row; and
        /// when a node's reject takes a link this world decided already (that row turns into an orphan, which a fresh queue lists again).
        /// </summary>
        internal bool CanClaim { get; private set; } = true;

        private bool Plain { get; } = IsPlain(Queue, Applied.Graph);

        internal static World Of(ReviewBasis basis, string who, DateTimeOffset at)
        {
            var (draft, overrides, onDraft) = basis;
            return new World(who, at, overrides, OverrideApplier.Apply(draft, overrides), ReviewBacklog.Build(string.Empty, draft, overrides), draft, onDraft);
        }

        internal Result<IReadOnlyList<MapOverride>> Plan(ReviewDecision decision) => decision.Verb switch
        {
            ReviewVerb.AcceptDeterministic => AcceptDeterministic(),
            ReviewVerb.Accept or ReviewVerb.Reject => One(decision.Link is { } link ? DecideLink(decision, link) : DecideNode(decision)),
            ReviewVerb.Undo => decision.UndoesAll is null ? One(Undo(decision)) : UndoAll(decision.UndoesAll),
            ReviewVerb.Drop => One(Drop(decision)),
            ReviewVerb.Repoint => Repoint(decision),
            ReviewVerb.Rename or ReviewVerb.Move or ReviewVerb.Merge or ReviewVerb.Add or ReviewVerb.Delete => One(Edit(decision)),
            _ => One(Stale($"{decision.Verb.ToString().ToLowerInvariant()} is not a review action")),
        };

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
            var entry = _links.GetValueOrDefault(link.Value)?.FirstOrDefault(e => !_claimed.Contains(e));
            return entry is null
                ? Stale($"link {link.Value} is not in {(OnDraft ? "the review queue" : "the current map")} (fix: {(OnDraft ? "list it again with 'sindri context map review'" : "find its id with 'sindri context filemap'")})")
                : Row(Verb(d), entry.Node) with { Link = link };
        }

        internal Result<MapOverride> DecideNode(ReviewDecision d)
        {
            var entry = d.Node is { } key && _nodes.TryGetValue(key.Value, out var queued) && !_claimed.Contains(queued) ? queued : null;
            if (entry is null)
            {
                return Stale($"{d.Node?.Value ?? "no node"} is not in the review queue (fix: list it again with 'sindri context map review')");
            }

            var row = Row(Verb(d), entry.Node);
            return d.Verb == ReviewVerb.Reject && Resolve(entry.Node) is { } node ? row with { Rejection = RejectionIdentity.For(node) } : row;
        }

        /// <summary>Takes out of the queue what an accept or a reject planned here takes out once it is applied: the decided entry, and for a node's reject the links that end at it (the applier removes the node with its edges).</summary>
        internal void Claim(MapOverride row)
        {
            if (!Plain)
            {
                CanClaim = false;
                return;
            }

            if (row.Link is { } link)
            {
                _ = _decidedLinks.Add(link.Value);
                if (_links.GetValueOrDefault(link.Value)?.FirstOrDefault(e => !_claimed.Contains(e)) is { Group: not ReviewGroup.Orphaned } live)
                {
                    _ = _claimed.Add(live);   // an orphaned link entry stays: deciding it again orphans the new row too
                }

                return;
            }

            if (_nodes.TryGetValue(row.Target.Value, out var entry))
            {
                _ = _claimed.Add(entry);
            }

            if (row is { Action: OverrideAction.Reject, Rejection: not null })
            {
                var links = _linksAt.GetValueOrDefault(row.Target.Value) ?? [];
                _claimed.UnionWith(links);
                if (links.Exists(e => _decidedLinks.Contains(e.Link!.Value.Value)))   // why: link entries
                {
                    CanClaim = false;
                }
            }
        }

        // Keys a claim can trust: each node key once, no alias key that is also a node's (the applier resolves the alias), each live link id once.
        private static bool IsPlain(ReviewQueueView queue, GraphSnapshot graph)
        {
            var links = new HashSet<string>(StringComparer.Ordinal);
            var nodes = new HashSet<string>(StringComparer.Ordinal);
            return queue.Entries.Where(e => e.Link is not null && e.Group != ReviewGroup.Orphaned).All(e => links.Add(e.Link!.Value.Value))   // why: filtered on Link
                && graph.Nodes.All(n => nodes.Add(n.Key.Value))
                && !graph.Aliases.Any(a => nodes.Contains(a.Alias.Value));
        }

        // The graph's own Resolve, by index: an alias names its target, and the first node with a key wins.
        private GraphNode? Resolve(NodeKey key)
        {
            _aliases ??= Graph.Aliases.DistinctBy(a => a.Alias.Value, StringComparer.Ordinal).ToDictionary(a => a.Alias.Value, a => a.Target, StringComparer.Ordinal);
            _graphNodes ??= Graph.Nodes.DistinctBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
            return _graphNodes.GetValueOrDefault(_aliases.TryGetValue(key.Value, out var target) ? target.Value : key.Value);
        }

        private static Dictionary<string, ReviewEntry> Index(IEnumerable<ReviewEntry> entries, Func<ReviewEntry, string> key) =>
            entries.DistinctBy(key, StringComparer.Ordinal).ToDictionary(key, StringComparer.Ordinal);

        // Each entry under every key it has, in queue order.
        private static Dictionary<string, List<ReviewEntry>> Group(IEnumerable<ReviewEntry> entries, Func<ReviewEntry, string[]> keys) =>
            entries.SelectMany(e => keys(e).Distinct(StringComparer.Ordinal).Select(k => (Key: k, Entry: e)))
                .GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(p => p.Entry).ToList(), StringComparer.Ordinal);

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
