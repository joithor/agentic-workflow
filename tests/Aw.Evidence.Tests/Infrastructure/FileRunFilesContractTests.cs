using Aw.Evidence.Core.Files;
using Aw.Evidence.Infrastructure.Files;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class FileRunFilesContractTests : RunFilesContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override string Root => _root.Path;

    protected override IRunFiles Create() => new FileRunFiles();

    public void Dispose() => _root.Dispose();
}
