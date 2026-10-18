using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The in-memory socket table passes the same contract as the /proc adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeUnixSocketsContractTests : UnixSocketsContract
{
    protected override IUnixSockets Create() => new FakeUnixSockets(new Dictionary<long, string> { [880001] = "/tmp/tmux-1000/t1" });

    protected override IUnixSockets CreateWithoutSource() => new FakeUnixSockets();
}
