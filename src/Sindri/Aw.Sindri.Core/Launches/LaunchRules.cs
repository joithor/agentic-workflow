namespace Aw.Sindri.Core.Launches;

/// <summary>The timing rules of a launch request (UI spec E-1). Pure.</summary>
public static class LaunchRules
{
    /// <summary>How long a request stays fresh: the launcher acts on it, or expires it as SND-LAUNCH-007, within this time.</summary>
    public static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(30);

    /// <summary>True when <paramref name="row"/> is a request still inside its deadline with no outcome (SND-LAUNCH-008 counts only these).</summary>
    /// <param name="row">The launch.</param>
    /// <param name="now">The time now.</param>
    /// <returns>Whether it is in flight.</returns>
    public static bool InFlight(LaunchRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.State == LaunchState.Requested && now - row.RequestedAt < Deadline;
    }
}
