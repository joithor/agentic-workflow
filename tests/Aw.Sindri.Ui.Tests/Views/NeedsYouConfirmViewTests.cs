using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The inbox bar's confirmation panel in a real window (needs-you slice 3): the typed box and the hold reach the bar, the panel follows the bar, and A moves focus to the control without approving.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouConfirmViewTests : FrameHost
{
    private const string Phrase = "approve profile 70e158";

    [Fact]
    public Task The_typed_box_follows_the_bar_takes_focus_on_A_and_its_press_reaches_the_bar() => OnUi(async () =>
    {
        Server.AppChallengeAnswer = Fresh(RouteAnswer.Api("workflow-challenges-app", "profile"));
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        shell.OpenNeedsYouInbox();
        shell.Inbox.Select("profile:drift");
        Settle(window);
        await Until(window, () => shell.Inbox.Bar.ShowsTyped, State(shell));
        Settle(window);

        Assert.True(Visible(window, "NeedsYouConfirm") && Visible(window, "NeedsYouTypedRow"));
        Assert.False(Visible(window, "NeedsYouHoldRow"));

        await shell.Inbox.Bar.PressApprove();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        Settle(window);
        Assert.True(Named<TextBox>(window, "NeedsYouTyped").IsFocused, Why(window, Named<TextBox>(window, "NeedsYouTyped"), shell));
        Assert.Empty(Server.ProfileApprovals);   // A approved nothing

        ((TypedConfirmBoxViewModel)Named<TypedConfirmBox>(window, "NeedsYouTypedBox").DataContext!).Text = Phrase;
        Named<Button>(window, "NeedsYouTypedButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Inbox.Bar.IsApproved, $"profileApprovals={Server.ProfileApprovals.Count} refusal={shell.Inbox.Bar.Refusal} phrase={shell.Inbox.Bar.TypedBox?.Phrase} text={shell.Inbox.Bar.TypedBox?.Text}; {State(shell)}");

        _ = Assert.Single(Server.ProfileApprovals);
        Assert.False(Visible(window, "NeedsYouConfirm"));
    });

    [Fact]
    public Task The_hold_follows_the_bar_and_a_complete_hold_reaches_the_bar() => OnUi(async () =>
    {
        Server.AppChallengeAnswer = Fresh(RouteAnswer.SpecShaped("challenges-app", "held"));
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
        Assert.True(Named<Button>(window, "NeedsYouHold").IsFocused, Why(window, Named<Button>(window, "NeedsYouHold"), shell));
        Assert.Empty(Server.Approvals);

        var hold = shell.Inbox.Bar.HoldBox!;
        Assert.True(hold.Begin());
        Clock.Advance(TimeSpan.FromMilliseconds(hold.MinHoldMs));
        Named<HoldButton>(window, "NeedsYouHoldControl").Step();
        await Until(window, () => shell.Inbox.Bar.IsApproved, $"approvals={Server.Approvals.Count} refusal={shell.Inbox.Bar.Refusal} phase={shell.Inbox.Bar.HoldBox?.IsHolding}; {State(shell)}");

        Assert.Equal(hold.MinHoldMs, Assert.Single(Server.Approvals).HeldMs);
    });

    // The goldens' challenges ran out on 2026-01-01, long before the test clock: a challenge that has run out is asked for again, never answered.
    private static RouteAnswer Fresh(RouteAnswer answer) => answer with { Body = answer.Body.Replace("2026-01-01T00:05:00", "2026-10-11T12:30:00", StringComparison.Ordinal) };

    private static string Why(Window window, Control target, ShellViewModel shell) =>
        $"focus is on {window.FocusManager?.GetFocusedElement()?.GetType().Name}; target visible={target.IsEffectivelyVisible} enabled={target.IsEffectivelyEnabled} focusable={target.Focusable} size={target.Bounds.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)}x{target.Bounds.Height.ToString(System.Globalization.CultureInfo.InvariantCulture)}; {State(shell)}";

    private static string State(ShellViewModel shell) =>
        $"mode={shell.Inbox.Bar.Mode} selected={shell.NeedsYou.SelectedId} card={shell.Inbox.Card?.Id} approveProfile={shell.Health.Catalog.Row("ApproveProfile")?.Ui} terminal={shell.Inbox.Bar.ShowsTerminal} hold={shell.Inbox.Bar.ShowsHold} typed={shell.Inbox.Bar.ShowsTyped} enabled={shell.Inbox.Bar.Enabled}";
}
