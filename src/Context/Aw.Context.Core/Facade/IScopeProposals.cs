using Aw.Context.Core.Engine;
using Aw.Context.Core.Proposals;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>A scoping run's surfaces as draft proposals, and their withdrawal (scoping spec C9): what Sindri calls in process to record a run, and nothing else of the engine.</summary>
public interface IScopeProposals
{
    /// <summary>
    /// Writes a scoping run's surfaces as 'propose' rows in the latest draft (opening one as a copy of current when there is none); never changes current.
    /// An empty surface list withdraws the run's undecided proposals instead: one 'undo' row each, ones a person accepted, merged, renamed or moved stay, and no draft is opened.
    /// </summary>
    /// <param name="proposal">The run's surfaces.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was written or withdrawn, or SND-CONTEXT-001/004/030/045.</returns>
    public ValueTask<Result<ProposeResult>> Propose(ScopeProposal proposal, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Where each surface a scoping run proposed stands: mapped, new or unmapped.</summary>
    /// <param name="run">The run's id.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view, or SND-CONTEXT-001/004/030.</returns>
    public ValueTask<Result<ProposalsView>> Proposals(string run, ContextScope scope, CancellationToken cancellationToken);
}
