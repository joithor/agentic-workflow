namespace Aw.Kernel.Telemetry;

/// <summary>The one-line rule every sink applies, so one record can never become two lines.</summary>
internal static class LogLine
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
