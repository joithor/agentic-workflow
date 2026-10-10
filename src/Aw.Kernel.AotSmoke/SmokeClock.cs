namespace Aw.Kernel.AotSmoke;

internal sealed class SmokeClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
