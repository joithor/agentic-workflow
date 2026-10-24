namespace Aw.Sindri.Core.EngineReads;

/// <summary>When an instance is stuck (UI screens spec §4.6, API spec §3.8, E-30): its only pending work is a signal or an update, which has waited more than five minutes, and no timer is running.</summary>
public static class EngineStuck
{
    /// <summary>How long a signal or update may wait before the instance is stuck.</summary>
    public static readonly TimeSpan After = TimeSpan.FromMinutes(5);

    /// <summary>Whether an instance with this wait and these timers is stuck.</summary>
    /// <param name="waiting">What it waits on, or null.</param>
    /// <param name="timers">How many timers it has running.</param>
    /// <param name="now">The time to measure the wait against.</param>
    /// <returns>True when it waits on a signal or update for <see cref="After"/> or longer and has no timer.</returns>
    public static bool Is(EngineWaiting? waiting, int timers, DateTimeOffset now) =>
        waiting is { } wait && (string.Equals(wait.Kind, EngineWaitKinds.Signal, StringComparison.Ordinal) || string.Equals(wait.Kind, EngineWaitKinds.Update, StringComparison.Ordinal))
        && timers == 0 && now - wait.Since >= After;
}
