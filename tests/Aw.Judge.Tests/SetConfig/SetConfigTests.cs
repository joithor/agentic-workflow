using Aw.Judge.Features.SetConfig;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SetConfig;

public sealed class SetConfigTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task SetConfig_sets_a_threshold_and_keeps_the_provider_and_prompt_sort_blocks()
    {
        var service = new SetConfigService();
        var request = A.SetConfigRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert sets_a_threshold_and_keeps_the_provider_and_prompt_sort_blocks");
    }
}
