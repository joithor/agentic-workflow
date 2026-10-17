namespace Aw.Sindri.Core.Launches;

/// <summary>Ends the sessions that went silent (API spec §8.1): one 'session.ended' with reason lost per launch without a heartbeat for <see cref="LaunchRules.LostAfter"/>.</summary>
public interface ILostSessions
{
    /// <summary>Checks the silent launches once.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many sessions it ended.</returns>
    public ValueTask<int> Sweep(CancellationToken cancellationToken);
}
