using System.Globalization;
using System.Runtime.InteropServices;

namespace Aw.Evidence.Infrastructure.Native;

/// <summary>
/// Points one file descriptor at another until disposed, then restores it. A child process started meanwhile inherits
/// the detoured descriptor, so a tool that writes progress to its stdout (the Playwright driver) can be sent to the
/// process's stderr, keeping stdout for the one JSON document. Process-wide: only a command that prints nothing else
/// meanwhile uses it.
/// </summary>
internal sealed class DescriptorDetour : IDisposable
{
    /// <summary>Standard output.</summary>
    internal const int StandardOutput = 1;

    /// <summary>Standard error.</summary>
    internal const int StandardError = 2;

    private readonly int _target;
    private int _saved;

    private DescriptorDetour(int target, int saved)
    {
        _target = target;
        _saved = saved;
    }

    /// <summary>Points <paramref name="target"/> at <paramref name="source"/>.</summary>
    /// <param name="target">The descriptor to detour.</param>
    /// <param name="source">Where it writes meanwhile.</param>
    /// <returns>The detour; disposing restores <paramref name="target"/>.</returns>
    /// <exception cref="IOException">The C library refused (a closed descriptor).</exception>
    internal static DescriptorDetour Redirect(int target, int source)
    {
        var saved = Libc.Duplicate(target);
        if (saved < 0)
        {
            throw Failure("dup", target);
        }

        if (Libc.DuplicateOnto(source, target) < 0)
        {
            var failure = Failure("dup2", target);
            _ = Libc.Close(saved);
            throw failure;
        }

        return new DescriptorDetour(target, saved);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        var saved = Interlocked.Exchange(ref _saved, -1);
        if (saved < 0)
        {
            return;
        }

        _ = Libc.DuplicateOnto(saved, _target);
        _ = Libc.Close(saved);
    }

    private static IOException Failure(string call, int descriptor) =>
        new(string.Create(CultureInfo.InvariantCulture, $"{call} of descriptor {descriptor} failed (errno {Marshal.GetLastPInvokeError()})"));
}
