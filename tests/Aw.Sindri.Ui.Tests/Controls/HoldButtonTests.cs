using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The hold-to-approve button (setup spec §2.2): the time is the server's, a short hold approves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class HoldButtonTests
{
    private readonly AppConfirmFixture _f = new();

    private HoldButtonViewModel Hold(int minHoldMs = 1500, bool reducedMotion = false)
    {
        var hold = new HoldButtonViewModel("ApproveAllRecipes", _f.Catalog(), _f.Clock, () => reducedMotion);
        hold.Offer(_f.Held(minHoldMs));
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

    [Fact]
    public void The_fill_is_full_at_the_served_time_and_the_hold_reports_the_time_held()
    {
        var hold = Hold(minHoldMs: 2000);
        _ = hold.Begin();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal((null, 0.5d), (hold.Tick(), hold.Progress));

        _f.Clock.Advance(TimeSpan.FromMilliseconds(1000));

        Assert.Equal(2000, hold.Tick());
        Assert.Equal((1d, false), (hold.Progress, hold.IsHolding));
    }

    [Fact]
    public void The_time_is_the_served_one_and_never_the_buttons_own()
    {
        var hold = Hold(minHoldMs: 800);
        _ = hold.Begin();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(800));

        Assert.Equal((800, "Hold to approve, 0.8 seconds"), (hold.Tick(), hold.Help));
    }

    [Fact]
    public void Letting_go_early_resets_the_fill_and_says_so_once()
    {
        var hold = Hold();
        var cancelled = 0;
        hold.Cancelled += (_, _) => cancelled++;
        _ = hold.Begin();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(600));
        _ = hold.Tick();

        hold.Cancel();
        hold.Cancel();

        Assert.Equal((0d, "Hold to approve, 1.5 seconds", 1), (hold.Progress, hold.Text, cancelled));
    }

    [Fact]
    public void A_repeated_start_is_ignored_and_the_start_is_announced_once_with_no_tick_announcements()
    {
        var hold = Hold();
        var started = 0;
        var said = 0;
        hold.Started += (_, _) => started++;
        hold.PropertyChanged += (_, e) => said += string.Equals(e.PropertyName, nameof(HoldButtonViewModel.Name), StringComparison.Ordinal) ? 1 : 0;

        Assert.True(hold.Begin());
        Assert.False(hold.Begin());
        _f.Clock.Advance(TimeSpan.FromMilliseconds(300));
        _ = hold.Tick();
        _ = hold.Tick();

        Assert.Equal((1, 0), (started, said));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void The_fill_shows_only_when_motion_is_not_reduced_and_the_words_count_either_way(bool reduced, bool fills)
    {
        var hold = Hold(reducedMotion: reduced);
        _ = hold.Begin();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(750));
        _ = hold.Tick();

        Assert.Equal((fills, "Keep holding, 0.8 of 1.5 seconds"), (hold.ShowsFill, hold.Text));
    }

    [Fact]
    public void A_complete_hold_says_approving_and_a_refused_one_goes_back_to_idle_with_a_fresh_start()
    {
        var hold = Hold();
        _ = hold.Begin();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1600));
        _ = hold.Tick();
        hold.Busy();
        Assert.Equal(("Approving...", false), (hold.Text, hold.Begin()));

        hold.Reset();

        Assert.True(hold.Begin());
    }

    [Theory]
    [InlineData("ApproveProfile")]
    [InlineData("ApproveRecipe")]
    [InlineData("ApproveAllRecipes")]
    public void The_three_approvals_are_accepted(string action) => Assert.Equal(action, new HoldButtonViewModel(action, _f.Catalog(), _f.Clock).Action);

    [Theory]
    [InlineData("Decide")]
    [InlineData("StopProcess")]
    [InlineData("")]
    public void Any_other_action_is_refused_at_construction(string action) =>
        _ = Assert.Throws<ArgumentException>(() => new HoldButtonViewModel(action, _f.Catalog(), _f.Clock));

    [Fact]
    public void A_row_that_is_not_ready_is_the_terminal_copy_and_takes_no_hold()
    {
        var hold = new HoldButtonViewModel("ApproveAllRecipes", _f.Catalog("cli-only:typed-confirmation"), _f.Clock);

        Assert.Equal((false, "sindri profile approve", false), (hold.IsReady, hold.Terminal?.Cli, hold.Begin()));
    }
}
