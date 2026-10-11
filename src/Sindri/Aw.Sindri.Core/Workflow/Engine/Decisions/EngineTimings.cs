namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>The profile's engine timings a decision needs (spec W19, W20, S1-D11).</summary>
/// <param name="DeskTimeout">The least a desk run's timeout may be ('workflow.deskTimeout', default 2 days); it always ends in a park.</param>
/// <param name="TakeoverIdle">How long a takeover waits before notifying the human once (2 hours).</param>
public sealed record EngineTimings(TimeSpan DeskTimeout, TimeSpan TakeoverIdle)
{
    /// <summary>The defaults: a 2-day desk timeout and a 2-hour takeover reminder.</summary>
    public static EngineTimings Default { get; } = new(TimeSpan.FromDays(2), TimeSpan.FromHours(2));
}
