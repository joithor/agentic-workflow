namespace Aw.Kernel.Telemetry;

/// <summary>
/// The <see cref="ILogSink"/> adapter over a <see cref="TextWriter"/>: standard error for CLIs and hosts, or a file the
/// host opened. Writes are serialized, so lines never interleave, and each line ends with '\n' on every platform.
/// </summary>
/// <param name="writer">The writer. The sink doesn't own it: the host disposes it.</param>
public sealed class TextWriterLogSink(TextWriter writer) : ILogSink
{
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public void WriteLine(string line)
    {
        LogLine.EnsureSingleLine(line);
        lock (_gate)
        {
            writer.Write(line);
            writer.Write('\n');
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
        lock (_gate)
        {
            writer.Flush();
        }
    }
}
