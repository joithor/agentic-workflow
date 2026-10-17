using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The fake snapshot passes the snapshot contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemorySessionSnapshotContractTests : SessionSnapshotContract
{
    protected override ISessionSnapshot Create() => new InMemorySessionSnapshot();
}
