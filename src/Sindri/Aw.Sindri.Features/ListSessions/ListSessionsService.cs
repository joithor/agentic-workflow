using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.ListSessions;

internal sealed class ListSessionsService(ISessionSnapshot snapshot, IAgentCatalog catalog, IApprovedProfile approvals, ICallerContextAccessor callers, TimeProvider clock)
    : IService<ListSessionsRequest, SessionList>
{
    public ValueTask<Result<SessionList>> Handle(ListSessionsRequest request, CancellationToken cancellationToken)
    {
        _ = (snapshot, catalog, approvals, callers, clock);
        throw new NotSupportedException();
    }
}
