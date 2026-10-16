using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests;

/// <summary>
/// A fake clock whose timer callbacks run on a thread-pool thread, as <see cref="TimeProvider.System"/>'s do. A callback that
/// touches a UI object without posting fails here the way it fails in the app. Advance returns once the callbacks it fired
/// have finished.
/// </summary>
internal sealed class OffThreadTimers(DateTimeOffset start) : FakeTimeProvider(start)
{
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        base.CreateTimer(s => Task.Run(() => callback(s), CancellationToken.None).GetAwaiter().GetResult(), state, dueTime, period);   // why: the fake fires inline; the pool thread is the point
}
