using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

[Lane(TestLane.Unit)]
public sealed class FakeToolRunnerContractTests : ToolRunnerContract
{
    internal override IToolRunner Create(ToolRun scripted) => new FakeToolRunner(scripted);
}
