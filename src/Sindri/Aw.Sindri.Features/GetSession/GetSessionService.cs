using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSession;

internal sealed class GetSessionService(ISessionSnapshot snapshot, IAgentCatalog catalog, IApprovedProfile approvals, ICallerContextAccessor callers)
    : IService<GetSessionRequest, SessionView>
{
    public ValueTask<Result<SessionView>> Handle(GetSessionRequest request, CancellationToken cancellationToken)
    {
        _ = (snapshot, catalog, approvals, callers);
        throw new NotSupportedException();
    }
}
