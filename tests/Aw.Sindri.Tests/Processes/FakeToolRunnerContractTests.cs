using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Processes;

[Lane(TestLane.Unit)]
public sealed class FakeToolRunnerContractTests : ToolRunnerContract
{
    internal override IToolRunner Create(ToolRun scripted) => new FakeToolRunner(scripted);
}
