using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The inbox bar's confirmation panel in a real window (needs-you slice 3): the typed box and the hold reach the bar, the panel follows the bar, and A moves focus to the control without approving.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouConfirmViewTests : FrameHost
{
    private const string Phrase = "approve profile 70e158";

    [Fact(Skip = "typed box: Dispatcher.UIThread.RunJobs() blocks after Select on a confirm row under headless; follow-up")]
    public Task The_typed_box_follows_the_bar_takes_focus_on_A_and_its_press_reaches_the_bar() => OnUi(async () =>
    {
        Server.AppChallengeAnswer = RouteAnswer.Api("workflow-challenges-app", "profile");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        shell.OpenNeedsYouInbox();
        shell.Inbox.Select("profile:drift");
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        await Until(window, () => shell.Inbox.Bar.ShowsTyped);
        Settle(window);

        Assert.True(Visible(window, "NeedsYouConfirm") && Visible(window, "NeedsYouTypedRow"));
        Assert.False(Visible(window, "NeedsYouHoldRow"));

        await shell.Inbox.Bar.PressApprove();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        Settle(window);
        Assert.True(Named<TextBox>(window, "NeedsYouTyped").IsFocused);
        Assert.Empty(Server.ProfileApprovals);   // A approved nothing

        ((TypedConfirmBoxViewModel)Named<TypedConfirmBox>(window, "NeedsYouTypedBox").DataContext!).Text = Phrase;
        Named<Button>(window, "NeedsYouTypedButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Inbox.Bar.IsApproved);

        _ = Assert.Single(Server.ProfileApprovals);
        Assert.False(Visible(window, "NeedsYouConfirm"));
    });

    [Fact(Skip = "hold: focus lands on a Loaded-priority post, IsFocused unproven under headless; follow-up")]
    public Task The_hold_follows_the_bar_and_a_complete_hold_reaches_the_bar() => OnUi(async () =>
    {
        Server.Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready");
        Server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-recipes");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        shell.OpenNeedsYouInbox();
        shell.Inbox.Select("setup:recipes");
        await Until(window, () => shell.Inbox.Bar.ShowsHold && shell.Inbox.Bar.HoldBox!.Gate is not null);
        Settle(window);

        Assert.True(Visible(window, "NeedsYouHoldRow"));
        Assert.False(Visible(window, "NeedsYouTypedRow"));

        await shell.Inbox.Bar.PressApprove();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        Settle(window);
        Assert.True(Named<Button>(window, "NeedsYouHold").IsFocused);
        Assert.Empty(Server.Approvals);

        var hold = shell.Inbox.Bar.HoldBox!;
        Assert.True(hold.Begin());
        Clock.Advance(TimeSpan.FromMilliseconds(hold.MinHoldMs));
        Named<HoldButton>(window, "NeedsYouHoldControl").Step();
        await Until(window, () => shell.Inbox.Bar.IsApproved);

        Assert.Equal(hold.MinHoldMs, Assert.Single(Server.Approvals).HeldMs);
    });

}
