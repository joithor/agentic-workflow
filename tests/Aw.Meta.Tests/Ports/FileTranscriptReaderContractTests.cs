using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class FileTranscriptReaderContractTests : TranscriptReaderContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    protected override ITranscriptReader Create() => new FileTranscriptReader();

    protected override string Write(string name, string content)
    {
        var path = Path.Combine(_dir.Path, name + ".jsonl");
        File.WriteAllText(path, content);
        return path;
    }

    protected override void Append(string path, string content) => File.AppendAllText(path, content);

    public void Dispose() => _dir.Dispose();
}
