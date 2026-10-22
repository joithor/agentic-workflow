using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0051 // temporary: bisect marks (remove with them)

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The inbox bar's confirmation panel in a real window (needs-you slice 3): the typed box and the hold reach the bar, the panel follows the bar, and A moves focus to the control without approving.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouConfirmViewTests : FrameHost
{
    private const string Phrase = "approve profile 70e158";

    [Fact]
    public Task The_typed_box_follows_the_bar_takes_focus_on_A_and_its_press_reaches_the_bar() => OnUi(async () =>
    {
        Mark("typed start");
        Server.AppChallengeAnswer = RouteAnswer.Api("workflow-challenges-app", "profile");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        Mark("typed connected");
        shell.OpenNeedsYouInbox();
        Mark("typed inbox open");
        shell.Inbox.Select("profile:drift");
        Mark("typed selected");
        Dispatcher.UIThread.RunJobs();
        Mark("typed jobs1");
        window.UpdateLayout();
        Mark("typed layout1");
        Dispatcher.UIThread.RunJobs();
        foreach (var pr in new[] { DispatcherPriority.Send, DispatcherPriority.Normal, DispatcherPriority.Loaded, DispatcherPriority.Render, DispatcherPriority.Background })
        {
            Mark("pre " + pr);
            Dispatcher.UIThread.RunJobs(pr);
            Mark("post " + pr);
        }

        Mark("layout");
        window.UpdateLayout();
        Mark("layout done");
        await Until(window, () => shell.Inbox.Bar.ShowsTyped);
        Mark("typed shows");
        Settle(window);
        Mark("typed settled");

        Assert.True(Visible(window, "NeedsYouConfirm") && Visible(window, "NeedsYouTypedRow"));
        Assert.False(Visible(window, "NeedsYouHoldRow"));

        Mark("typed press");
        await shell.Inbox.Bar.PressApprove();
        Mark("typed pressed");
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        Settle(window);
        Mark("typed settled2");
        Assert.True(Named<TextBox>(window, "NeedsYouTyped").IsFocused);
        Assert.Empty(Server.ProfileApprovals);   // A approved nothing

        ((TypedConfirmBoxViewModel)Named<TypedConfirmBox>(window, "NeedsYouTypedBox").DataContext!).Text = Phrase;
        Named<Button>(window, "NeedsYouTypedButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Mark("typed clicked");
        await Until(window, () => shell.Inbox.Bar.IsApproved);
        Mark("typed approved");

        _ = Assert.Single(Server.ProfileApprovals);
        Assert.False(Visible(window, "NeedsYouConfirm"));
    });

    [Fact]
    public Task The_hold_follows_the_bar_and_a_complete_hold_reaches_the_bar() => OnUi(async () =>
    {
        Mark("hold start");
        Server.Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready");
        Server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-recipes");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        shell.OpenNeedsYouInbox();
        Mark("hold connected");
        shell.Inbox.Select("setup:recipes");
        await Until(window, () => shell.Inbox.Bar.ShowsHold && shell.Inbox.Bar.HoldBox!.Gate is not null);
        Mark("hold shows");
        Settle(window);
        Mark("hold settled");

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

    private static void Mark(string s) => System.IO.File.AppendAllText("/home/thor/repos/aw-wt/sindri-ui-needs-you-3/ny3-marks.txt", s + "\n");
}
