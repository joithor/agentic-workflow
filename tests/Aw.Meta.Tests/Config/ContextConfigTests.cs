using Aw.Meta.Core.Config;

namespace Aw.Meta.Tests.Config;

public sealed class ContextConfigTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_recycle_threshold_defaults_to_300000_and_must_be_positive()
    {
        var team = new TeamConfig("t1", "/work", ["meta", "api"], "meta");

        Assert.Equal(300_000, team.Context.RecycleAt);
        Assert.Null(ThresholdChecks.Problem(team));
        Assert.Contains("context.recycleAt", ThresholdChecks.Problem(team with { Context = new ContextConfig(0) }), StringComparison.Ordinal);
    }
}
