namespace Aw.Meta.Features.Watch;

/// <summary>The Watch request: poll until the duration is over, emitting a line per settled change.</summary>
/// <param name="Interval">The time between polls.</param>
/// <param name="Duration">How long this watch runs before it asks to be re-armed.</param>
/// <param name="Emit">Receives each line as it happens (the CLI prints and flushes it).</param>
public sealed record WatchRequest(TimeSpan Interval, TimeSpan Duration, Func<string, ValueTask> Emit);
