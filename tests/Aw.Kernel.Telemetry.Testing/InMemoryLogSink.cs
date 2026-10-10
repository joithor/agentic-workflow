using System.Text;

namespace Aw.Kernel.Telemetry.Testing;

/// <summary>The in-memory <see cref="ILogSink"/> for tests: keeps every line, in order. Passes <see cref="LogSinkContract"/>.</summary>
public sealed class InMemoryLogSink : ILogSink
{
    private readonly Lock _gate = new();
    private readonly StringBuilder _text = new();
    private readonly List<string> _lines = [];

    /// <summary>The lines written so far, in order.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_gate)
            {
                return [.. _lines];
            }
        }
    }

    /// <summary>Everything written so far, each line ending with '\n'.</summary>
    public string Text
    {
        get
        {
            lock (_gate)
            {
                return _text.ToString();
            }
        }
    }

    /// <inheritdoc />
    public void WriteLine(string line)
    {
        LogLines.EnsureSingleLine(line);
        lock (_gate)
        {
            _lines.Add(line);
            _text.Append(line).Append('\n');
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
    }
}
