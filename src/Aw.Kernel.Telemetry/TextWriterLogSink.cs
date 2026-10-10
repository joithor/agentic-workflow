namespace Aw.Kernel.Telemetry;

/// <summary>
/// The <see cref="ILogSink"/> adapter over a <see cref="TextWriter"/>: standard error for CLIs and hosts, or a file the
/// host opened. Writes are serialized, so lines never interleave, and each line ends with '\n' on every platform.
/// </summary>
/// <param name="writer">The writer. The sink doesn't own it: the host disposes it.</param>
public sealed class TextWriterLogSink(TextWriter writer) : ILogSink
{
    /// <inheritdoc />
    public void WriteLine(string line) => throw new NotSupportedException($"red: write the line to {writer.GetType().Name}");

    /// <inheritdoc />
    public void Flush() => throw new NotSupportedException("red: flush");
}
