using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

[Lane(TestLane.Unit)]
public sealed class FakeProcessAncestryContractTests : ProcessAncestryContract
{
    protected override IProcessAncestry Create() => FakeProcessAncestry.Under(Environment.ProcessId, "zsh", "claude", "login");

    protected override ValueTask<int> GonePid() => ValueTask.FromResult(999);
}
