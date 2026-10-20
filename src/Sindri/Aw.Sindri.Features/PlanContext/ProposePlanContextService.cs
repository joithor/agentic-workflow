using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.PlanContext;

/// <summary>LN-CTX1: after the owner's approve, the design's surfaces go to the map as proposals (C9's port, run = the plan instance) and stop 2's answers as owner decision records (C14's ingest).</summary>
internal sealed class ProposePlanContextService : IService<ProposePlanContextRequest, ProposePlanContextResponse>
{
    /// <summary>Holds the two ports.</summary>
    /// <param name="proposals">The run's proposals on the context map.</param>
    /// <param name="decisions">The owner's answers as decision records.</param>
    public ProposePlanContextService(IScopeRunProposals proposals, IOwnerDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(decisions);
    }

    public ValueTask<Result<ProposePlanContextResponse>> Handle(ProposePlanContextRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
