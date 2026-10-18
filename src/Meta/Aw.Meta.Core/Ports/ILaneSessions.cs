using Aw.Kernel;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Ports;

/// <summary>
/// The session a Claude lane runs now. Claude Code keeps the current id in ~/.claude/sessions/&lt;pid&gt;.json and changes it on /clear,
/// whereas lane-sessions/&lt;lane&gt; is written only by the hooks of non-Claude lanes.
/// </summary>
public interface ILaneSessions
{
    /// <summary>Finds the current session id of the Claude running in a lane's tmux session.</summary>
    /// <param name="session">The tmux session name ("team-lane").</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The id, or null when there is no such session, no Claude under its pane, or no record of it.</returns>
    public ValueTask<Result<SessionId?>> Current(string session, CancellationToken cancellationToken);
}
