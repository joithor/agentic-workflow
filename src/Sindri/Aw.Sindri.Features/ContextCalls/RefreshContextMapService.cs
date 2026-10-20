using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The work of a refresh job (owner F1, 2026-10-19): build, clean up, promote and record it.</summary>
internal sealed class RefreshContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events, IJobContextAccessor jobs, TimeProvider clock)
    : IService<RefreshContextMapRequest, RefreshContextMapResult>
{
    public ValueTask<Result<RefreshContextMapResult>> Handle(RefreshContextMapRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("F1: the refresh is not built yet");
}
