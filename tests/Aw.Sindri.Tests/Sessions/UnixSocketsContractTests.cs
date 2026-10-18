using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The /proc/net/unix adapter passes the unix socket contract on the fixture tree.</summary>
[UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class UnixSocketsContractTests : UnixSocketsContract
{
    protected override IUnixSockets Create() => new UnixSockets(SessionFixtures.Proc);

    protected override IUnixSockets CreateWithoutSource() => new UnixSockets(Path.Combine(SessionFixtures.Root, "no-such-proc-root"));
}
