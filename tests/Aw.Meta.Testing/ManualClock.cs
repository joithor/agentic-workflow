namespace Aw.Meta.Testing;

/// <summary>A clock that moves only when told to.</summary>
/// <param name="start">The first reading.</param>
public sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => _now += by;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
