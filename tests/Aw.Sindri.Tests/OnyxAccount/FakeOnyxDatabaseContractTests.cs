using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

[Lane(TestLane.Unit)]
public sealed class FakeOnyxDatabaseContractTests : OnyxDatabaseContract
{
    protected override IOnyxDatabase Create(int? group) => new FakeOnyxDatabase { Group = group };
}
