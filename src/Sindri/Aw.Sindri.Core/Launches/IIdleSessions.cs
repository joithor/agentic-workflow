namespace Aw.Sindri.Core.Launches;

/// <summary>Ends the Step sessions whose agent ended its turn with no report (see <see cref="LaunchRules.IdleAfter"/>): one 'session.ended' with reason idle per launch.</summary>
public interface IIdleSessions
{
    /// <summary>Checks the launches with a turn-ended file once.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many sessions it ended.</returns>
    public ValueTask<int> Sweep(CancellationToken cancellationToken);
}
