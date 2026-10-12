namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Probe measurements: sent as xUnit diagnostics and, when AW_SPIKE_LOG names a file, appended to it (the raw source the doc cites).</summary>
internal static class SpikeLog
{
    private static readonly Lock _gate = new();

    /// <summary>Records <paramref name="line"/>.</summary>
    /// <param name="line">The measurement.</param>
    public static void Write(string line)
    {
        TestContext.Current.SendDiagnosticMessage(line);
        if (Environment.GetEnvironmentVariable("AW_SPIKE_LOG") is { Length: > 0 } path)
        {
            lock (_gate)
            {
                File.AppendAllLines(path, [line]);
            }
        }
    }
}
