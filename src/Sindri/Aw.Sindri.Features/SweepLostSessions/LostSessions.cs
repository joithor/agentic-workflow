using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>Ends the sessions with no heartbeat for <see cref="LaunchRules.LostAfter"/> as 'session.ended' with reason lost (API spec §8.1).</summary>
/// <param name="liveness">The heartbeats.</param>
/// <param name="session">The session events.</param>
/// <param name="clock">The time.</param>
internal sealed class LostSessions(LaunchLiveness liveness, LaunchSessionEvents session, TimeProvider clock) : ILostSessions
{
    public ValueTask<int> Sweep(CancellationToken cancellationToken)
    {
        _ = (liveness, session, clock);
        throw new NotSupportedException("LostSessions: make LostSessionsTests pass");
    }
}
