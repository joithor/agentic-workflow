namespace Aw.Redaction.Tests;

/// <summary>A clock that moves forward by a fixed step every time it is read, so a whole-scan deadline is reached
/// after a known number of matches, whatever the machine's speed.</summary>
internal sealed class SteppingClock(TimeSpan step) : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks += step.Ticks;
}
