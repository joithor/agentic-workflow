using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Features.BuildMap;
using Aw.Context.Features.GetProposals;
using Aw.Context.Features.ProposeFromScope;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The build and scoping-proposal half of the map facade (S-4).</summary>
internal sealed class MapProposals(
    IService<BuildMapRequest, MapBuildReport> build,
    IService<ProposeFromScopeRequest, ProposeResult> propose,
    IService<GetProposalsRequest, ProposalsView> proposals)
{
    public ValueTask<Result<MapBuildReport>> BuildMap(IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken) =>
        build.Handle(new BuildMapRequest(progress, scope), cancellationToken);

    public ValueTask<Result<ProposeResult>> Propose(ScopeProposal proposal, ContextScope scope, CancellationToken cancellationToken) =>
        propose.Handle(new ProposeFromScopeRequest(proposal, scope), cancellationToken);

    public ValueTask<Result<ProposalsView>> Proposals(string run, ContextScope scope, CancellationToken cancellationToken) =>
        proposals.Handle(new GetProposalsRequest(run, scope), cancellationToken);
}
