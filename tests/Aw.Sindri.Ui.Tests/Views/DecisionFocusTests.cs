using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Focus on the decision sheet's options across re-reads, a press and a refusal (review run 1, K7 / corr-5, a11y-2; UI spec §11.2: focus is never lost).</summary>
[Lane(TestLane.Integration)]
public sealed class DecisionFocusTests : FrameHost
{
    private const string Pending = "DEC-00000000000000000000000000000011";

    private async Task<(Window Window, ShellViewModel Shell)> WithDecisionOpen(double width = 1400)
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: width);
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, Pending));
        Settle(window);
        return (window, shell);
    }

    private static Button Option(Window window, string label) =>
        Named<Control>(window, "DecisionSheet").GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("decision-option") && string.Equals((string?)b.Content, label, StringComparison.Ordinal));

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    [Fact]
    public Task A_re_read_keeps_the_option_the_user_tabbed_to() => OnUi(async () =>
    {
        var (window, shell) = await WithDecisionOpen();
        var decline = Option(window, "Decline");
        Assert.True(decline.Focus(NavigationMethod.Tab));

        shell.ShowNeedsYou(new NeedsYouFeed([.. shell.NeedsYou.Seen.Rows.Select(r => r.Entry)], ""));
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);

        Assert.Same(decline, Focused(window));
    });

    [Fact]
    public Task Pressing_an_option_under_a_covering_sheet_leaves_focus_on_a_usable_control() => OnUi(async () =>
    {
        var (window, shell) = await WithDecisionOpen(width: 900);
        var accept = Option(window, "Accept");
        Assert.True(accept.Focus(NavigationMethod.Tab));

        accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await shell.Decision.Settled;
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);

        var focused = Focused(window);
        Assert.NotNull(focused);
        Assert.True(focused.IsEffectivelyEnabled && focused.IsEffectivelyVisible);
        Assert.True(Named<Control>(window, "Sheet").IsVisualAncestorOf(focused));
    });

    [Fact]
    public Task A_refusal_gives_focus_back_with_the_keyboards_ring() => OnUi(async () =>
    {
        Server.Decided = RouteAnswer.Problem("decisions-id", "decided-elsewhere");
        var (window, shell) = await WithDecisionOpen();
        var accept = Option(window, "Accept");
        Assert.True(accept.Focus(NavigationMethod.Tab));

        accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await shell.Decision.Settled;
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);

        var focused = Assert.IsType<Button>(Focused(window));
        Assert.Contains(":focus-visible", focused.Classes, StringComparer.Ordinal);
    });
}
