using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class AppendRunningLogContractTests : RunningLogContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    private string File_ => Path.Combine(_dir.Path, "nested", "digests", "log.md");

    protected override IRunningLog Create() => new AppendRunningLog(_ => ValueTask.FromResult(File_));

    protected override IReadOnlyList<string> Read(IRunningLog log) => File.ReadAllLines(File_);

    public void Dispose() => _dir.Dispose();
}
