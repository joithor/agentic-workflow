using Aw.Kernel.Testing;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Tests.Items;

/// <summary>The 400s of the item routes echo the value they refused: never as raw control characters (API spec §3.6, review #229 SEC-3).</summary>
[Lane(TestLane.Unit)]
public sealed class ItemRouteTests
{
    private const string Hostile = "x\u001b[2J\ny";

    [Fact]
    public void A_refused_id_is_echoed_escaped_and_on_one_line()
    {
        var error = ItemRoute.Item(Hostile).Error;

        Assert.DoesNotContain('\u001b', error.Message);
        Assert.DoesNotContain('\n', error.Message);
        Assert.Contains("\\x1B[2J", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refused_state_is_echoed_escaped_and_on_one_line()
    {
        var error = ItemRoute.State(Hostile).Error;

        Assert.DoesNotContain('\u001b', error.Message);
        Assert.DoesNotContain('\n', error.Message);
    }

    [Fact]
    public void A_refused_repo_is_echoed_escaped_and_on_one_line()
    {
        var error = ItemRoute.Repo(Hostile).Error;

        Assert.DoesNotContain('\u001b', error.Message);
        Assert.DoesNotContain('\n', error.Message);
    }
}
