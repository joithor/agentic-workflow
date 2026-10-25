using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Parsing;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Parsing;

[Trait("Lane", "integration")]
public sealed class FileParseQuarantineContractTests : ParseQuarantineContract, IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aw-quarantine-" + RealTime.NewGuid().ToString("N"));

    protected override IParseQuarantine Create() => new FileParseQuarantine(_directory);

    protected override IParseQuarantine Restart(IParseQuarantine previous) => new FileParseQuarantine(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
