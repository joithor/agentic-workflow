using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Features;

/// <summary>
/// <see cref="IScopeRunProposals"/> over the proposal command's own service, run in process without the command chain (the chain belongs to a caller's
/// request: the Step is the engine acting as the service), so the draft rows and the 'context.map.proposed' event are what 'context map propose' writes.
/// </summary>
/// <param name="service">The proposal command's service.</param>
internal sealed class ScopeRunProposals(ProposeContextMapService service) : IScopeRunProposals
{
    /// <inheritdoc/>
    public ValueTask<Result<ProposeContextMapResponse>> Replace(string run, RepoName repo, IReadOnlyList<ProposedSurface> surfaces, CancellationToken cancellationToken) =>
        service.Handle(new ProposeContextMapRequest(run, repo.Value, surfaces), cancellationToken);
}
