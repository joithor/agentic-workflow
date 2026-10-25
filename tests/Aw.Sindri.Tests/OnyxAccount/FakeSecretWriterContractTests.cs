using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

[Lane(TestLane.Unit)]
public sealed class FakeSecretWriterContractTests : SecretWriterContract
{
    protected override (ISecretWriter Writer, ISecretPointers Pointers) Create()
    {
        var fake = new FakeSecretWriter();
        return (fake, fake);
    }
}
