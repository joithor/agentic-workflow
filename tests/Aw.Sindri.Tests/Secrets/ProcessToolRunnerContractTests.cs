using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

// The real runner over harmless system tools (printf, sh, sleep); never security or op.
[Lane(TestLane.Integration)]
public sealed class ProcessToolRunnerContractTests : ToolRunnerContract
{
    internal override IToolRunner Create(ToolRun scripted) => new ProcessToolRunner();
}
