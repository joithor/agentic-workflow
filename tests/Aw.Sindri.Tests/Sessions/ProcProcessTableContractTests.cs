using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The /proc adapter passes the process table contract on the fixture tree.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcProcessTableContractTests : ProcessTableContract
{
    protected override IProcessTable Create() => new ProcProcessTable(SessionFixtures.Proc, SessionFixtures.OwnUid);
}
