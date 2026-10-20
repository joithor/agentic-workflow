using Aw.Kernel;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Packets;

/// <summary><see cref="IOwnerDecisions"/> until the context engine has C14's ingest (scoping spec C14: "skipped until it exists"): it stores nothing and says why, which the Step reports.</summary>
internal sealed class PendingOwnerDecisions : IOwnerDecisions
{
    public ValueTask<Result<OwnerDecisionsIngested>> Ingest(InstanceAddress run, string packet, IReadOnlyList<OwnerDecision> decisions, CancellationToken cancellationToken) => throw new NotSupportedException();
}
