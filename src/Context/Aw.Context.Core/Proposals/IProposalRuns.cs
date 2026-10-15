using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Proposals;

/// <summary>
/// What the store remembers of a scoping run beyond its 'propose' rows (spec §8.7): every surface the run submitted, the mapped and
/// unmapped ones too, so the run's graph status can list them all. It sits beside the graph store, which it shares a database with.
/// </summary>
public interface IProposalRuns
{
    /// <summary>Remembers every surface a scoping run submitted, the mapped and unmapped ones too, so its graph status can list them all later (spec §8.7). A second save of the same run replaces the first.</summary>
    /// <param name="proposal">The run and its surfaces.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many surfaces are remembered, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<int>> SaveRun(ScopeProposal proposal, CancellationToken cancellationToken);

    /// <summary>Writes a scoping run's 'propose' rows and its surface list in one transaction: both are kept or neither (a busy database never leaves the rows without the run's list).</summary>
    /// <param name="proposal">The run and its surfaces.</param>
    /// <param name="rows">The 'propose' rows the planner made; one already stored is kept once.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many rows were added, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<int>> Propose(ScopeProposal proposal, IReadOnlyList<MapOverride> rows, CancellationToken cancellationToken);

    /// <summary>The run as last saved: its repo and every surface it submitted.</summary>
    /// <param name="run">The run's address.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The run, null when it was never saved, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<ScopeProposal?>> Run(string run, CancellationToken cancellationToken);
}
