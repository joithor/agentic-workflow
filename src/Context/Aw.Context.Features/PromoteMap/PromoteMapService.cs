using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PromoteMap;

/// <summary>
/// Makes the latest draft the current map (spec §8.7), when nothing in it is left to decide (SND-CONTEXT-046: no draft, or
/// undecided review items; a skipped item isn't decided). The proposals a person accepted become nodes of the version first,
/// because a 'propose' row only adds its node to a draft and a pending one is never part of the current map. Sindri checks
/// the caller and the typed confirmation first (protected).
/// </summary>
internal sealed class PromoteMapService(IGraphStore store, ICurrentUser user, TimeProvider clock) : IService<PromoteMapRequest, MapPromoted>
{
    public async ValueTask<Result<MapPromoted>> Handle(PromoteMapRequest request, CancellationToken cancellationToken)
    {
        if (ReviewDrafts.Gate(request.Scope, "promoting a draft") is { } refused)
        {
            return refused;
        }

        var found = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } draft)
        {
            return PromoteReadiness.Check(queue: null).Error;
        }

        // The queue the review shows: an item a deny glob hides is neither decidable nor blocking, and the stored map loses what the globs hide.
        var seen = ReviewDrafts.Visible(draft, request.Scope);
        if (seen.IsError)
        {
            return seen.Error;
        }

        var ready = PromoteReadiness.Check(seen.Value.Queue);
        return ready.IsError ? ready.Error : await Promote(draft, seen.Value.View, request.Scope, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<MapPromoted>> Promote(MapSnapshot draft, MapView view, ContextScope scope, CancellationToken cancellationToken)
    {
        var previous = await store.Current(cancellationToken).ConfigureAwait(false);
        var number = previous.IsError ? previous.Error : await WithAcceptedProposals(draft, view, cancellationToken).ConfigureAwait(false);
        var promoted = number.IsError ? number.Error : await store.Promote(number.Value, cancellationToken).ConfigureAwait(false);
        var undone = promoted.IsError ? Unit.Value : await UndoCutDecisions(draft, view, scope, cancellationToken).ConfigureAwait(false);
        return promoted.IsError
            ? promoted.Error
            : undone.IsError ? undone.Error : new MapPromoted(promoted.Value, previous.Value is { } old ? old with { Status = MapVersionStatus.Retained } : null);
    }

    // The draft's own number, or the number of a copy of it that holds the accepted proposals' nodes and none of what the deny globs hide. The copy replaces only the draft that was read:
    // if a rebuild replaced it meanwhile the promote is refused (046), and the rebuild's draft and any build still being written stay.
    private async ValueTask<Result<int>> WithAcceptedProposals(MapSnapshot draft, MapView view, CancellationToken cancellationToken)
    {
        var proposed = OverrideLog.Active(view.Overrides).Where(o => o.Action == OverrideAction.Propose).Select(o => o.Target.Value).ToHashSet(StringComparer.Ordinal);
        var nodes = view.Applied.Nodes.Where(n => proposed.Contains(n.Key.Value) && view.Graph.Resolve(n.Key) is null).ToList();
        if (nodes.Count == 0 && !view.Filtered)
        {
            return draft.Version.Number;
        }

        var edges = view.Applied.Edges.Where(e => proposed.Contains(e.From.Value) && !view.Graph.Edges.Any(e.SameFact));
        var joined = view.Graph with { Nodes = [.. view.Graph.Nodes, .. nodes], Edges = [.. view.Graph.Edges, .. edges] };
        var saved = await store.ReplaceDraft(draft.Version.Number, new MapImport(draft.Version.Repo, draft.Version.Commit, joined, []), draft.Version.Created, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : saved.Value.Number;
    }

    // The decisions about what the promote cut are undone with it, so the next draft has no orphan to drop that the globs made.
    private async ValueTask<Result<Unit>> UndoCutDecisions(MapSnapshot draft, MapView view, ContextScope scope, CancellationToken cancellationToken)
    {
        var who = ReviewDrafts.Who(scope.Caller, user);
        var at = clock.GetUtcNow();
        MapOverride[] rows = [.. view.CutDecisions(draft.Overrides).Select(o => new MapOverride(OverrideAction.Undo, o.Target, who, at, Undoes: o.ToId()))];
        if (rows.Length == 0)
        {
            return Unit.Value;
        }

        var appended = await store.AppendOverrides(rows, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
