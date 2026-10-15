using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.Views.Components;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The lock button under the headless platform: its label and title, the popover it opens, Esc, and Copy.</summary>
[Lane(TestLane.Integration)]
public sealed class ProtectedActionButtonTests
{
    private const string Subject = "REQ-01JD4";
    private const string Cli = "sindri decision accept REQ-01JD4";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ProtectedActionButtonTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, ProtectedActionButton Control, Button Lock, FakeClipboard Clipboard) Show()
    {
        var clipboard = new FakeClipboard();
        var vm = new ProtectedActionViewModel(new ProtectedAction("Accept", Cli, Subject), clipboard, new FakeTimeProvider(A.Noon));
        var control = new ProtectedActionButton { DataContext = vm };
        var window = new Window { Content = control, Width = 600, Height = 300 };
        window.Show();
        Settle(window);
        return (window, control, Named<Button>(control, "LockButton"), clipboard);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Visual root, string name)
        where T : Control => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static void Press(Window window, Key key, PhysicalKey physical) => window.KeyPress(key, RawInputModifiers.None, physical, keySymbol: null);

    [Fact]
    public async Task The_button_reads_the_lock_label_has_the_title_and_a_name_with_the_subject()
    {
        var (label, tip, name) = await OnUi(() =>
        {
            var (window, _, button, _) = Show();
            var result = (button.Content?.ToString(), ToolTip.GetTip(button)?.ToString(), AutomationProperties.GetName(button));
            window.Close();
            return result;
        });

        Assert.Equal("⌂ Accept · terminal", label);
        Assert.Equal("Needs a typed confirmation at a terminal", tip);
        Assert.Equal($"Accept {Subject}: needs a typed confirmation at a terminal", name);
    }

    [Fact]
    public async Task Click_opens_a_popover_with_the_served_cli_Copy_and_Open_in_an_agent()
    {
        var (open, cli, copy, copyName, agent, agentName, pending) = await OnUi(() =>
        {
            var (window, control, button, _) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var root = (Visual)((Flyout)button.Flyout!).Content!;
            _ = control;
            var result = (button.Flyout.IsOpen, Named<TextBlock>(root, "ServedCli").Text, Named<Button>(root, "CopyButton").Content?.ToString(),
                AutomationProperties.GetName(Named<Button>(root, "CopyButton")), Named<Control>(root, "OpenInAgent").IsEffectivelyVisible,
                AutomationProperties.GetName(Named<Control>(root, "OpenInAgent")), Named<Control>(root, "OpenInAgentChip").IsEffectivelyVisible);
            _ = Named<Button>(root, "OpenInAgent");   // a Button, so it is a control a screen reader can reach
            window.Close();
            return result;
        });

        Assert.True(open);
        Assert.Equal(Cli, cli);
        Assert.Equal("Copy", copy);
        Assert.Equal($"Copy: {Cli}", copyName);
        Assert.True(agent);
        Assert.Equal($"Open in an agent: {Subject}, available with API P10a", agentName);
        Assert.True(pending);
    }

    // The popover's focus targets are the controls a screen reader and the Tab key reach (a11y-0): each is a control
    // element with a name, and Open in an agent is a disabled Button, not a focusable panel.
    [Fact]
    public async Task The_popovers_controls_are_exposed_with_names_and_Open_in_an_agent_is_a_disabled_button()
    {
        var rows = await OnUi(() =>
        {
            var (window, _, button, _) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var root = (Visual)((Flyout)button.Flyout!).Content!;
            var agent = Named<Button>(root, "OpenInAgent");
            var result = new[] { Named<Button>(root, "CopyButton"), agent }
                .Select(c => (c.Name, Control: ControlAutomationPeer.CreatePeerForElement(c).IsControlElement(), AutomationProperties.GetName(c), c.IsEnabled)).ToList();
            window.Close();
            return result;
        });

        Assert.All(rows, r => Assert.True(r.Control));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Item3)));
        Assert.Equal(("OpenInAgent", false), (rows[1].Name, rows[1].IsEnabled));
    }

    [Fact]
    public async Task Copy_puts_the_served_cli_on_the_clipboard()
    {
        var (text, label) = await OnUi(() =>
        {
            var (window, _, button, clipboard) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var root = (Visual)((Flyout)button.Flyout!).Content!;
            Named<Button>(root, "CopyButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Settle(window);
            var result = (clipboard.Text, Named<Button>(root, "CopyButton").Content?.ToString());
            window.Close();
            return result;
        });

        Assert.Equal(Cli, text);
        Assert.Equal("Copied", label);
    }

    [Fact]
    public async Task Esc_closes_the_popover_and_returns_focus()
    {
        var (closed, focused) = await OnUi(() =>
        {
            var (window, _, button, _) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            Press(window, Key.Escape, PhysicalKey.Escape);
            Settle(window);
            var result = (button.Flyout?.IsOpen == false, button.IsFocused);
            window.Close();
            return result;
        });

        Assert.True(closed);
        Assert.True(focused);
    }
}
