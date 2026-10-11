using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Auth;

[Lane(TestLane.Unit)]
public sealed class InMemoryHumanTokenStoreContractTests : HumanTokenStoreContract
{
    protected override IHumanTokenStore Create() => new InMemoryHumanTokenStore();
}
