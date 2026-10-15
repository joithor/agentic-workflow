using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IDelay: waiting moves a manual clock instead of sleeping; kept honest by DelayContract.</summary>
/// <param name="clock">The clock that time passes on.</param>
public sealed class InMemoryDelay(ManualClock clock) : IDelay
{
    private readonly List<TimeSpan> _waits = [];

    /// <summary>Every wait asked for, in order.</summary>
    public IReadOnlyList<TimeSpan> Waits => _waits;

    /// <inheritdoc />
    public ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _waits.Add(duration);
        clock.Advance(duration);
        return ValueTask.CompletedTask;
    }
}
