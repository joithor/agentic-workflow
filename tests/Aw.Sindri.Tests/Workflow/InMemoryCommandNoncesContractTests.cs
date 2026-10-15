using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryCommandNoncesContractTests : CommandNoncesContract
{
    protected override ICommandNonces Create(FakeTimeProvider clock) => new InMemoryCommandNonces(clock);
}
