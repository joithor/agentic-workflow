using Aw.Evidence.Core.Files;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryRunFilesContractTests : RunFilesContract
{
    protected override IRunFiles Create() => new InMemoryRunFiles();

    protected override string Root => "/runs";
}
