using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The real client over the canned handler: Sindri's wire format, with no socket (unit lane).</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientContractTests : SindriApiContract
{
    private protected override ISindriApi Reach(FakeSindriApi server) => new SindriApiClient(new HttpClient(new FakeSindriHandler(server)), new InMemoryHumanToken(A.HumanToken), TimeProvider.System);
}
