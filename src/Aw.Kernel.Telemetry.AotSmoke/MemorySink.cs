namespace Aw.Kernel.Telemetry.AotSmoke;

/// <summary>Keeps the JSON lines so the smoke can check them.</summary>
internal sealed class MemorySink : ILogSink
{
    public List<string> Lines { get; } = [];

    public void WriteLine(string line) => Lines.Add(line);

    public void Flush()
    {
    }
}
