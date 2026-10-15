using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Proposals;

/// <summary>Decides which of a scoping run's surfaces become 'propose' rows (S-4): the new ones, once, and never one a person rejected. Pure.</summary>
public static class ProposalPlanner
{
    /// <summary>Plans the rows for one proposal.</summary>
    /// <param name="proposal">What the run posted.</param>
    /// <param name="basis">The graph the rows would join: the latest draft, or the current map when there is none.</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="who">Who proposes.</param>
    /// <param name="at">When.</param>
    /// <returns>The new rows and what was left out.</returns>
    public static ProposalPlan Plan(ScopeProposal proposal, GraphSnapshot basis, IReadOnlyList<MapOverride> overrides, string who, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(basis);
        var applied = OverrideApplier.Apply(basis, overrides).Graph;
        var rejects = OverrideLog.Active(overrides).Where(o => o.Action == OverrideAction.Reject).ToList();
        var undone = OverrideLog.Undone(overrides);
        var rows = new List<MapOverride>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var (existing, rejected, unmapped) = (0, 0, 0);
        foreach (var surface in proposal.Surfaces)
        {
            if (ProposalNodes.KeyOf(surface) is not { } key)
            {
                unmapped++;
            }
            else if (applied.Resolve(key) is not null || !seen.Add(key.Value))
            {
                existing++;
            }
            else if (IsRejected(rejects, key, surface))
            {
                rejected++;
            }
            else
            {
                rows.Add(WithGeneration(Row(proposal, surface, key, who, at), Generation(overrides, undone, proposal.Run, key)));
            }
        }

        return new ProposalPlan(rows, existing, rejected, unmapped);
    }

    private static bool IsRejected(List<MapOverride> rejects, NodeKey key, ProposedSurface surface)
    {
        var identity = RejectionIdentity.For(ProposalNodes.Candidate(key, surface));
        return rejects.Exists(r => r.Target.Value == key.Value && (r.Rejection is null || r.Rejection == identity));
    }

    // The undo of a run's propose row is respected, not overwritten: asking again is a new row, whose id carries how many were undone before it.
    private static int Generation(IReadOnlyList<MapOverride> overrides, IReadOnlySet<string> undone, string run, NodeKey key) =>
        overrides.Count(o => o.Action == OverrideAction.Propose && o.Target.Value == key.Value && o.Proposal?.Run == run && undone.Contains(o.ToId().Value));

    private static MapOverride Row(ScopeProposal proposal, ProposedSurface surface, NodeKey key, string who, DateTimeOffset at) =>
        new(OverrideAction.Propose, key, who, at, surface.Name,
            Proposal: new ProposalPayload(proposal.Run, proposal.Repo, surface.Kind ?? key.Kind, surface.Parent, surface.Evidence, surface.Quote));

    private static MapOverride WithGeneration(MapOverride row, int generation) =>
        generation == 0 ? row : row with { Proposal = row.Proposal! with { Generation = generation } };   // why: Row always sets the payload
}
