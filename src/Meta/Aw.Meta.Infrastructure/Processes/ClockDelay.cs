using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>IDelay over the clock the host was composed with.</summary>
/// <param name="clock">The clock; real time in the CLI.</param>
internal sealed class ClockDelay(TimeProvider clock) : IDelay
{
    public async ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken) =>
        await Task.Delay(duration, clock, cancellationToken).ConfigureAwait(false);
}
