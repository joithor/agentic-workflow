using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.SessionHeartbeat;

[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "--slice-only: unused until this branch registers the slice; remove with that registration")]
internal sealed class SessionHeartbeatService : IService<SessionHeartbeatRequest, SessionHeartbeatResponse>
{
    public ValueTask<Result<SessionHeartbeatResponse>> Handle(SessionHeartbeatRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SessionHeartbeat: make SessionHeartbeatTests pass");
}
