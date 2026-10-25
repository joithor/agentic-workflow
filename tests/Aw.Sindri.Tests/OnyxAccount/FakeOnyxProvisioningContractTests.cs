using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

[Lane(TestLane.Unit)]
public sealed class FakeOnyxProvisioningContractTests : OnyxProvisioningContract
{
    protected override (IOnyxProvisioning Provisioning, Func<int> Logouts) Create()
    {
        var fake = new FakeOnyxProvisioning();
        return (fake, () => fake.LoggedOut);
    }
}
