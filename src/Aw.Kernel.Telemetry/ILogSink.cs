namespace Aw.Kernel.Telemetry;

/// <summary>
/// Where JSON log lines go (the port for the log destination: standard error, a file, a test buffer). Each call writes
/// one complete line; the logger has already scrubbed it. Implementations are thread-safe: concurrent writers never
/// interleave inside a line. Every sink passes the contract suite in Aw.Kernel.Telemetry.Testing.
/// </summary>
public interface ILogSink
{
    /// <summary>Writes one JSON line (without its line terminator; the sink adds '\n').</summary>
    /// <param name="line">The line.</param>
    public void WriteLine(string line);

    /// <summary>Pushes buffered lines to the destination.</summary>
    public void Flush();
}
