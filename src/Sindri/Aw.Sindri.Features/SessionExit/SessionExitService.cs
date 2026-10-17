using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.SessionExit;

/// <summary>
/// The agent's exit (API spec §8.1): checks the launch secret, records the session id the call carries (before the end), appends 'session.ended' with
/// reason exit and the code, and deletes the launch's hash and per-launch files, so a late call from a dead launch can't validate.
/// </summary>
/// <param name="session">The session events and the secret check.</param>
internal sealed class SessionExitService(LaunchSessionEvents session) : IService<SessionExitRequest, SessionExitResponse>
{
    public ValueTask<Result<SessionExitResponse>> Handle(SessionExitRequest request, CancellationToken cancellationToken)
    {
        _ = session;
        throw new NotSupportedException("SessionExit: make SessionExitTests pass");
    }
}
