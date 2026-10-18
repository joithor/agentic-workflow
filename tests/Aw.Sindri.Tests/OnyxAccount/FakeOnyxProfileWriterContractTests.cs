using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

[Lane(TestLane.Unit)]
public sealed class FakeOnyxProfileWriterContractTests : OnyxProfileWriterContract
{
    protected override IOnyxProfileWriter Create() => new FakeOnyxProfileWriter();
}
