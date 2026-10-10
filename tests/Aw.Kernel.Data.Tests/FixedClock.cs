namespace Aw.Kernel.Data.Tests;

/// <summary>A clock the tests set by hand.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
