using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Confirmations;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The typed confirmation (setup spec §2.1): the phrase is the server's text, and only exactly it presses the button.</summary>
[Lane(TestLane.Unit)]
public sealed class TypedConfirmBoxTests
{
    private readonly AppConfirmFixture _f = new();

    [Theory]
    [InlineData("approve profile 3f9a1c", true)]
    [InlineData("  approve profile 3f9a1c  ", true)]
    [InlineData("Approve profile 3f9a1c", false)]
    [InlineData("approve profile 3f9a1", false)]
    [InlineData("approve profile 3f9a1cc", false)]
    [InlineData("", false)]
    public void Only_the_exact_phrase_enables_the_button(string typed, bool enabled)
    {
        var box = new TypedConfirmBoxViewModel("ApproveProfile", _f.Catalog(), _f.Clock);
        box.Offer(_f.Typed());

        box.Text = typed;

        Assert.Equal(enabled, box.CanSubmit);
    }

    private TypedConfirmBoxViewModel Box(string action = "ApproveProfile")
    {
        var box = new TypedConfirmBoxViewModel(action, _f.Catalog(), _f.Clock);
        box.Offer(_f.Typed());
        return box;
    }

    [Fact]
    public void The_phrase_is_the_servers_text_verbatim_and_a_held_challenges_alternative_when_it_has_one()
    {
        var typed = Box();
        var held = new TypedConfirmBoxViewModel("ApproveAllRecipes", _f.Catalog(), _f.Clock);
        held.Offer(_f.Held());

        Assert.Equal(("approve profile 3f9a1c", "approve-all 1c1fd4"), (typed.Phrase, held.Phrase));
    }

    [Fact]
    public void A_match_shows_the_tick_and_a_wrong_phrase_shows_the_mismatch_instead()
    {
        var box = Box();

        box.Text = "approve profile 3f9";
        Assert.Equal((false, true), (box.Matches, box.ShowsMismatch));
        box.Text = "approve profile 3f9a1c";

        Assert.Equal((true, false), (box.Matches, box.ShowsMismatch));
    }

    [Fact]
    public void A_press_on_the_exact_phrase_gives_the_trimmed_text_and_a_press_on_a_wrong_one_is_said_once()
    {
        var box = Box();
        box.Text = "nope";
        Assert.Null(box.Submit());
        Assert.Equal("That doesn't match. Type exactly: approve profile 3f9a1c", box.Live.Announcement);

        box.Text = " approve profile 3f9a1c ";

        Assert.Equal("approve profile 3f9a1c", box.Submit());
    }

    [Fact]
    public void One_press_sends_once_the_field_empties_and_the_button_goes_off_until_the_answer_comes_back()
    {
        var box = Box();
        box.Text = "approve profile 3f9a1c";

        box.SetSending(sending: true);
        Assert.Equal(("", false, null), (box.Text, box.CanSubmit, box.Submit()));

        box.SetSending(sending: false);
        box.Text = "approve profile 3f9a1c";

        Assert.True(box.CanSubmit);
    }

    [Theory]
    [InlineData("ApproveProfile", "Approve settings")]
    [InlineData("ApproveRecipe", "Approve workflow")]
    public void The_button_says_what_it_approves(string action, string label) => Assert.Equal(label, Box(action).ButtonLabel);

    [Theory]
    [InlineData("Decide")]
    [InlineData("StopProcess")]
    [InlineData("")]
    public void Any_other_action_is_refused_at_construction(string action) =>
        _ = Assert.Throws<ArgumentException>(() => new TypedConfirmBoxViewModel(action, _f.Catalog(), _f.Clock));

    [Fact]
    public void A_row_that_is_not_ready_is_the_terminal_copy_of_the_served_command()
    {
        var box = new TypedConfirmBoxViewModel("ApproveProfile", _f.Catalog("cli-only:typed-confirmation"), _f.Clock);

        Assert.Equal((false, "sindri profile approve"), (box.IsReady, box.Terminal?.Cli));
    }

    [Fact]
    public void Typing_the_phrase_says_it_matches_once_on_the_change_and_never_per_key()
    {
        var box = Box();
        var resting = box.Live.Announcement;

        box.Text = "approve profile 3f9";
        Assert.Equal((resting, false), (box.Live.Announcement, box.Live.IsAnnouncing));
        box.Text = "approve profile 3f9a1c";
        Assert.Equal(("Matches", true), (box.Live.Announcement, box.Live.IsAnnouncing));

        box.Live.Quiet();
        box.Text = "approve profile 3f9a1c ";   // still a match: no new announcement
        Assert.False(box.Live.IsAnnouncing);
    }
}
