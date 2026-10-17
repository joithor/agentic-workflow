using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.SessionHeartbeat;

/// <summary>
/// The session's heartbeat (API spec §8.1): checks the launch secret, starts the 120 s clock again and, the first time a heartbeat carries the agent's own
/// session id, records it as 'launch.session-reported'.
/// </summary>
/// <param name="session">The session events and the secret check.</param>
internal sealed class SessionHeartbeatService(LaunchSessionEvents session) : IService<SessionHeartbeatRequest, SessionHeartbeatResponse>
{
    public ValueTask<Result<SessionHeartbeatResponse>> Handle(SessionHeartbeatRequest request, CancellationToken cancellationToken)
    {
        _ = session;
        throw new NotSupportedException("SessionHeartbeat: make SessionHeartbeatTests pass");
    }
}
