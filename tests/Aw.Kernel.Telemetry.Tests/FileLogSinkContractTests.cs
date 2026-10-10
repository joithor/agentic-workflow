using Aw.Kernel.Telemetry.Testing;
using Aw.Kernel.Testing;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Integration)]
public sealed class FileLogSinkContractTests : LogSinkContract, IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("aw-telemetry-sink-").FullName;
    private readonly Dictionary<ILogSink, (string Path, StreamWriter Writer)> _files = [];

    protected override ILogSink CreateSink()
    {
        var path = Path.Combine(_directory, $"{_files.Count}.jsonl");
        var writer = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var sink = new TextWriterLogSink(writer);
        _files[sink] = (path, writer);
        return sink;
    }

    // Read with the file system, not the sink: the bytes on disk are what a reader sees.
    protected override string Delivered(ILogSink sink)
    {
        using var stream = new FileStream(_files[sink].Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        foreach (var (_, writer) in _files.Values)
        {
            writer.Dispose();
        }

        Directory.Delete(_directory, recursive: true);
    }
}
