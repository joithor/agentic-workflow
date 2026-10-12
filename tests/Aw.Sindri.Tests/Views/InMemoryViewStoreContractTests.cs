using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

[Lane(TestLane.Unit)]
public sealed class InMemoryViewStoreContractTests : ViewStoreContract
{
    protected override (IViewStore Store, IViewReader Reader) Create()
    {
        var store = new InMemoryViewStore(AwJson.CreateOptions(SindriJsonContext.Default));
        return (store, store);
    }
}
