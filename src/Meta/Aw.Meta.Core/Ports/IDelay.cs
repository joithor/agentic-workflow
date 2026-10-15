namespace Aw.Meta.Core.Ports;

/// <summary>Waits between polls, so the watch's loop can run without real time in tests.</summary>
public interface IDelay
{
    /// <summary>Waits.</summary>
    /// <param name="duration">How long.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the time has passed.</returns>
    public ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken);
}
