using Aw.Sindri.Features.GetLaunchTargets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetLaunchTargets;

public sealed class GetLaunchTargetsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetLaunchTargets_returns_the_probed_targets()
    {
        var service = new GetLaunchTargetsService();
        var request = A.GetLaunchTargetsRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert returns_the_probed_targets");
    }
}
