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
        ArgumentNullException.ThrowIfNull(overrides);
        _ = (who, at);
        throw new NotSupportedException("S-4: ProposalPlanner.Plan is not built yet");
    }
}
