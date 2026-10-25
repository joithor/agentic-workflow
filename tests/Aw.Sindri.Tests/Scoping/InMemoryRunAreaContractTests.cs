using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The in-memory run area honours the run-area contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryRunAreaContractTests : RunAreaContract
{
    protected override IRunArea Create() => new InMemoryRunArea();
}
