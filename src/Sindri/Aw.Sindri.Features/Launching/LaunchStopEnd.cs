using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.Launching;

/// <summary>Ends a live launch's session as an exit when the launcher stops it, so the launch stops being live at once and the silence sweep never marks it lost.</summary>
/// <param name="session">The session events: the one 'session.ended' path.</param>
/// <param name="views">The 'launches' view: only a live launch is ended.</param>
internal sealed class LaunchStopEnd(LaunchSessionEvents session, ILaunchViews views)
{
    /// <summary>Ends the launch's session when its row is live.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the reads and the append.</param>
    /// <returns>Done.</returns>
    public ValueTask EndIfLive(LaunchId launch, CancellationToken cancellationToken)
    {
        _ = (session, views, launch, cancellationToken);   // red: nothing is recorded yet
        return ValueTask.CompletedTask;
    }
}
