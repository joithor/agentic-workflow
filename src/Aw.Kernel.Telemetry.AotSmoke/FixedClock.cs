namespace Aw.Kernel.Telemetry.AotSmoke;

/// <summary>A clock stopped at one instant, so the smoke's output is the same on every run.</summary>
internal sealed class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);
}
