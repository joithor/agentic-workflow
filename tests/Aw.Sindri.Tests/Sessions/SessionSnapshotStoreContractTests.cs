using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The real snapshot store passes the snapshot contract.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionSnapshotStoreContractTests : SessionSnapshotContract
{
    protected override ISessionSnapshot Create() => new SessionSnapshotStore();
}
