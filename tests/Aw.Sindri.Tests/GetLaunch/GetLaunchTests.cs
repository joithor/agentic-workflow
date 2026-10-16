using Aw.Sindri.Features.GetLaunch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetLaunch;

public sealed class GetLaunchTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetLaunch_returns_the_launch_view()
    {
        var service = new GetLaunchService();
        var request = A.GetLaunchRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert returns_the_launch_view");
    }
}
