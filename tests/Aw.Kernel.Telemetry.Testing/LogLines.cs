namespace Aw.Kernel.Telemetry.Testing;

/// <summary>The one-line rule every fake sink applies, the same check the real sinks make.</summary>
internal static class LogLines
{
    public static void EnsureSingleLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.AsSpan().IndexOfAny('\n', '\r') >= 0)
        {
            throw new ArgumentException("A log line must not contain a line break.", nameof(line));
        }
    }
}
