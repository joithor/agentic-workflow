using System.ComponentModel;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// What a failed read of the OS motion setting looks like, in one place: <see cref="GnomeMotion"/> turns it into "no
/// reduction", and <see cref="PolledMotionPreference"/> (which runs readers on a pool thread, where an unhandled exception
/// ends the process) keeps the last value when any reader fails this way.
/// </summary>
internal static class MotionReadFailure
{
    /// <summary>Starting a process failed: no such command (<see cref="Win32Exception"/>), a deleted working directory (<see cref="IOException"/>) or a process that already exited.</summary>
    /// <param name="error">The exception thrown by a read.</param>
    /// <returns>True when the read failed in an expected way.</returns>
    internal static bool Is(Exception error) => error is Win32Exception or IOException or InvalidOperationException;
}
