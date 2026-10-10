using Aw.Judge.Core.Config;
using Aw.Judge.Features.GetConfig;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.GetConfig;

public sealed class GetConfigTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetConfig_returns_the_effective_config()
    {
        var stored = JudgeConfig.Default with { Providers = new ProvidersConfig(["codex-cli"], Jev: null) };
        var store = new InMemoryJudgeConfigStore { Stored = stored };

        var result = await new GetConfigService(store).Handle(A.GetConfigRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(stored, result.Value.Config);
    }
}
