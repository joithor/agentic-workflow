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
internal sealed class PromoteMapService(IGraphStore store) : IService<PromoteMapRequest, MapPromoted>
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

        var ready = PromoteReadiness.Check(found.Value is { } draft ? ReviewBacklog.Build(draft.Label, draft.Graph, draft.Overrides) : null);
        return ready.IsError ? ready.Error : await Promote(found.Value!, cancellationToken).ConfigureAwait(false);   // why: PromoteReadiness refused a missing draft
    }

    private async ValueTask<Result<MapPromoted>> Promote(MapSnapshot draft, CancellationToken cancellationToken)
    {
        var previous = await store.Current(cancellationToken).ConfigureAwait(false);
        var number = previous.IsError ? previous.Error : await WithAcceptedProposals(draft, cancellationToken).ConfigureAwait(false);
        var promoted = number.IsError ? number.Error : await store.Promote(number.Value, cancellationToken).ConfigureAwait(false);
        return promoted.IsError
            ? promoted.Error
            : new MapPromoted(promoted.Value, previous.Value is { } old ? old with { Status = MapVersionStatus.Retained } : null);
    }

    // The draft's own number, or the number of a copy of it that holds the accepted proposals' nodes.
    private async ValueTask<Result<int>> WithAcceptedProposals(MapSnapshot draft, CancellationToken cancellationToken)
    {
        var proposed = OverrideLog.Active(draft.Overrides).Where(o => o.Action == OverrideAction.Propose).Select(o => o.Target.Value).ToHashSet(StringComparer.Ordinal);
        var applied = OverrideApplier.Apply(draft.Graph, draft.Overrides).Graph;
        var nodes = applied.Nodes.Where(n => proposed.Contains(n.Key.Value) && draft.Graph.Resolve(n.Key) is null).ToList();
        if (nodes.Count == 0)
        {
            return draft.Version.Number;
        }

        var edges = applied.Edges.Where(e => proposed.Contains(e.From.Value) && !draft.Graph.Edges.Any(e.SameFact));
        var joined = draft.Graph with { Nodes = [.. draft.Graph.Nodes, .. nodes], Edges = [.. draft.Graph.Edges, .. edges] };
        var saved = await store.SaveDraft(new MapImport(draft.Version.Repo, draft.Version.Commit, joined, []), draft.Version.Created, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : saved.Value.Number;
    }
}
