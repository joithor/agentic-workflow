using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Proposals;

/// <summary>Decides which of a scoping run's surfaces become 'propose' rows (S-4): the new ones, once, and never one a person rejected. Pure.</summary>
public static class ProposalPlanner
{
    /// <summary>Plans the rows for one proposal against a graph no deny glob applies to (internal: a caller plans against <see cref="MapAccess.View"/>, which cuts what the globs hide).</summary>
    /// <param name="proposal">What the run posted.</param>
    /// <param name="basis">The graph the rows would join: the latest draft, or the current map when there is none.</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="who">Who proposes.</param>
    /// <param name="at">When.</param>
    /// <returns>The new rows and what was left out.</returns>
    internal static ProposalPlan Plan(ScopeProposal proposal, GraphSnapshot basis, IReadOnlyList<MapOverride> overrides, string who, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(overrides);
        return Plan(proposal, new MapView(basis, OverrideApplier.Apply(basis, overrides).Graph, overrides, cut: null), who, at);
    }

    /// <summary>Plans the rows for one proposal against what the caller may see (<see cref="MapAccess.View"/>): a surface a hidden node matches is new, and a reject names it by either evidence.</summary>
    /// <param name="proposal">What the run posted.</param>
    /// <param name="view">The latest draft (or the current map when there is none) as the caller may see it, with pending proposals.</param>
    /// <param name="who">Who proposes.</param>
    /// <param name="at">When.</param>
    /// <returns>The new rows and what was left out.</returns>
    public static ProposalPlan Plan(ScopeProposal proposal, MapView view, string who, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(view);
        var (applied, overrides) = (view.Applied, view.Overrides);
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
            else if (IsRejected(rejects, key, surface, view))
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

    /// <summary>
    /// Plans the withdrawal of a run's undecided proposals (scoping spec C9): one 'undo' row for each of its 'propose' rows that no accept, reject, merge or delete has decided since.
    /// The ones a person accepted, merged, renamed or moved stay (Kept counts them); a rejected one is decided already and needs no row. Pure.
    /// </summary>
    /// <param name="run">The scoping run's id; no other run's rows are touched.</param>
    /// <param name="repo">The run's repo: a run id is the scoping run's own, so only this repo's rows of it are touched.</param>
    /// <param name="overrides">Every override of the draft, as the caller may see them.</param>
    /// <param name="who">Who withdraws.</param>
    /// <param name="at">When.</param>
    /// <returns>The undo rows and how many accepted proposals stay.</returns>
    public static (IReadOnlyList<MapOverride> Rows, int Kept) Withdraw(string run, ContextRepoName repo, IReadOnlyList<MapOverride> overrides, string who, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(overrides);
        var active = OverrideLog.Active(overrides);
        var (rows, kept) = (new List<MapOverride>(), 0);
        foreach (var proposed in active.Where(o => o.Action == OverrideAction.Propose && string.Equals(o.Proposal?.Run, run, StringComparison.Ordinal) && o.Proposal?.Repo == repo))
        {
            var standing = Standing(proposed, active);
            if (standing == ProposalStanding.Kept)
            {
                kept++;
            }
            else if (standing == ProposalStanding.Undecided)
            {
                rows.Add(new MapOverride(OverrideAction.Undo, proposed.Target, who, at) with { Undoes = proposed.ToId() });
            }
        }

        return (rows, kept);
    }

    private enum ProposalStanding
    {
        Undecided,
        Kept,
        Closed,
    }

    // What a person did with a proposal since it was written: accept, merge, rename or move keeps it; reject or delete closed it; nothing leaves it undecided.
    private static ProposalStanding Standing(MapOverride proposed, IReadOnlyList<MapOverride> active)
    {
        var since = active.Where(o => o.Target.Value == proposed.Target.Value && o.At >= proposed.At).Select(o => o.Action).ToList();
        if (since.Exists(a => a is OverrideAction.Accept or OverrideAction.Merge))
        {
            return ProposalStanding.Kept;
        }

        if (since.Exists(a => a is OverrideAction.Reject or OverrideAction.Delete))
        {
            return ProposalStanding.Closed;
        }

        return since.Exists(a => a is OverrideAction.Rename or OverrideAction.Move) ? ProposalStanding.Kept : ProposalStanding.Undecided;
    }

    // A reject names the proposal by the evidence it had when it was written: all of it before a deny glob, what the glob leaves after.
    private static bool IsRejected(List<MapOverride> rejects, NodeKey key, ProposedSurface surface, MapView view)
    {
        var whole = RejectionIdentity.For(ProposalNodes.Candidate(key, surface));
        var allowed = RejectionIdentity.For(ProposalNodes.Candidate(key, surface with { Evidence = view.AllowedEvidence(surface.Evidence) }));
        return rejects.Exists(r => r.Target.Value == key.Value && (r.Rejects(whole) || r.Rejects(allowed)));
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
