using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryTranscriptReaderContractTests : TranscriptReaderContract
{
    private readonly InMemoryTranscriptReader _reader = new();

    protected override ITranscriptReader Create() => _reader;

    protected override string Write(string name, string content)
    {
        _reader.Write(name, content);
        return name;
    }

    protected override void Append(string path, string content) => _reader.Append(path, content);
}
