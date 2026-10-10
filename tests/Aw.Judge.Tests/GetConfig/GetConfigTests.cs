using Aw.Judge.Features.GetConfig;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.GetConfig;

public sealed class GetConfigTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetConfig_returns_the_effective_config()
    {
        var service = new GetConfigService();
        var request = A.GetConfigRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert returns_the_effective_config");
    }
}
