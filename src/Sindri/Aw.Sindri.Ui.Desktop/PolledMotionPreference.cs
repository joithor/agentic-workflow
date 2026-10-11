using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop adapter of <see cref="IMotionPreference"/>: an OS reader, re-read on a timer.</summary>
/// <param name="read">Reads the OS setting (true: reduce motion).</param>
/// <param name="clock">The clock the timer runs on.</param>
/// <param name="interval">How often to re-read.</param>
internal sealed class PolledMotionPreference(Func<bool> read, TimeProvider clock, TimeSpan interval) : IMotionPreference, IDisposable
{
    public bool ReduceMotion => throw new NotSupportedException($"{read.Method.Name}{clock}{interval}");

    public event EventHandler? Changed
    {
        add => throw new NotSupportedException();
        remove => throw new NotSupportedException();
    }

    public void Dispose()
    {
    }
}
