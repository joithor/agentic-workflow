namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// How a scheduled run ended when it didn't fail (API spec C8). The scheduler records an idle run only when it is the
/// first ok after a failure, or the service's outcome isn't known to be ok.
/// </summary>
/// <param name="Idle">True when the run did nothing: it found no work and changed nothing.</param>
public sealed record ScheduledResult(bool Idle)
{
    /// <summary>The run did work.</summary>
    public static ScheduledResult Worked { get; } = new(Idle: false);

    /// <summary>The run did nothing.</summary>
    public static ScheduledResult Quiet { get; } = new(Idle: true);
}
