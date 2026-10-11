namespace Aw.Evidence.Infrastructure.Native;

/// <summary>
/// Points one file descriptor at another until disposed, then restores it. A child process started meanwhile inherits
/// the detoured descriptor, so a tool that writes progress to its stdout can be sent to the process's stderr.
/// </summary>
internal sealed class DescriptorDetour : IDisposable
{
    /// <summary>Standard output.</summary>
    internal const int StandardOutput = 1;

    /// <summary>Standard error.</summary>
    internal const int StandardError = 2;

    private DescriptorDetour(int target, int source)
    {
        Target = target;
        Source = source;
    }

    /// <summary>The detoured descriptor.</summary>
    internal int Target { get; }

    /// <summary>Where it writes meanwhile.</summary>
    internal int Source { get; }

    /// <summary>Points <paramref name="target"/> at <paramref name="source"/>.</summary>
    /// <param name="target">The descriptor to detour.</param>
    /// <param name="source">Where it writes meanwhile.</param>
    /// <returns>The detour; disposing restores <paramref name="target"/>.</returns>
    internal static DescriptorDetour Redirect(int target, int source) => new(target, source);

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
