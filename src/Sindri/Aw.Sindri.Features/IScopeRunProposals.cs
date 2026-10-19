using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Features;

/// <summary>A scoping run's proposals on the context map as the Steps write them (scoping spec C9): the run's surfaces replace its earlier ones, and none withdraws them.</summary>
internal interface IScopeRunProposals
{
    /// <summary>Writes the run's surfaces as draft proposals; the empty list withdraws the run's undecided ones.</summary>
    /// <param name="run">The run's id (the instance's address).</param>
    /// <param name="repo">The repo the surfaces were found in.</param>
    /// <param name="surfaces">The surfaces; none withdraws.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was written, with each surface's graph status, or the error.</returns>
    public ValueTask<Result<ProposeContextMapResponse>> Replace(string run, RepoName repo, IReadOnlyList<ProposedSurface> surfaces, CancellationToken cancellationToken);
}
