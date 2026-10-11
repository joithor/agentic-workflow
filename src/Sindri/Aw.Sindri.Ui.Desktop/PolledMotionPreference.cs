using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The desktop adapter of <see cref="IMotionPreference"/>: the OS reader (<see cref="Composition.MotionReader"/>), read at
/// start and re-read on a timer, raising <see cref="Changed"/> (on the timer's thread) when the answer differs. The reader
/// is the seam: tests drive a stand-in, never the OS setting.
/// </summary>
internal sealed class PolledMotionPreference : IMotionPreference, IDisposable
{
    private readonly Lock _gate = new();
    private readonly Func<bool> _read;
    private readonly ITimer _timer;
    private bool _reduce;

    /// <summary>Reads the setting now, then every <paramref name="interval"/>.</summary>
    /// <param name="read">Reads the OS setting (true: reduce motion).</param>
    /// <param name="clock">The clock the timer runs on.</param>
    /// <param name="interval">How often to re-read.</param>
    internal PolledMotionPreference(Func<bool> read, TimeProvider clock, TimeSpan interval)
    {
        _read = read;
        _reduce = read();
        _timer = clock.CreateTimer(static self => ((PolledMotionPreference)self!).Refresh(), this, interval, interval); // why: the state is this instance
    }

    public event EventHandler? Changed;

    public bool ReduceMotion
    {
        get
        {
            lock (_gate)
            {
                return _reduce;
            }
        }
    }

    public void Dispose() => _timer.Dispose();

    private void Refresh()
    {
        var now = _read();
        lock (_gate)
        {
            if (now == _reduce)
            {
                return;
            }

            _reduce = now;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
