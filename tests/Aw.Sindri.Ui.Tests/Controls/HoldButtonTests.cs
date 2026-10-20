using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The hold-to-approve button (setup spec §2.2): the time is the server's, a short hold approves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class HoldButtonTests
{
    private readonly AppConfirmFixture _f = new();

    private HoldButtonViewModel Hold()
    {
        var hold = new HoldButtonViewModel("ApproveAllRecipes", _f.Catalog(), _f.Clock);
        hold.Offer(_f.Held());
        return hold;
    }

    [Fact]
    public void A_hold_shorter_than_the_served_time_approves_nothing()
    {
        var hold = Hold();
        Assert.True(hold.Begin());
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1400));

        var done = hold.Tick();
        hold.Cancel();

        Assert.Equal((null, false), (done, hold.IsHolding));
    }
}
