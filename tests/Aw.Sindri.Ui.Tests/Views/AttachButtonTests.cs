using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.Views.Components;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The Attach component under the headless platform (screens spec §4.5): one template in two sizes, names, the chip, the menu's keys.</summary>
[Lane(TestLane.Integration)]
public sealed class AttachButtonTests
{
    private const string Address = "ITM-01JE7/project/1";
    private const string Note = "Attach joins the live session; your first keystroke takes it over.";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AttachButtonTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, AttachButton Button, AttachControlViewModel Vm) Show(AttachInput input, bool compact = false)
    {
        var vm = new AttachControlViewModel(input);
        var button = new AttachButton { DataContext = vm, IsCompact = compact };
        var window = new Window { Content = button, Width = 600, Height = 200 };
        window.Show();
        Settle(window);
        return (window, button, vm);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Control root, string name)
        where T : Control => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static AttachInput Ready => new(Address, AttachStepKind.Agent, AttachReady: true);

    [Fact]
    public async Task Two_sizes_share_one_template()
    {
        var signatures = await OnUi(() =>
        {
            static string Signature(bool compact)
            {
                var (window, button, _) = Show(Ready, compact);
                var s = string.Join('|', button.GetVisualDescendants().OfType<Control>().Select(c => $"{c.GetType().Name}:{c.Name}"));
                window.Close();
                return s;
            }

            return (Signature(compact: false), Signature(compact: true));
        });

        Assert.Contains("AttachPrimary", signatures.Item1, StringComparison.Ordinal);
        Assert.Equal(signatures.Item1, signatures.Item2);
        _ = Assert.Single(UiSource.Axaml, f => Path.GetFileName(f).Contains("Attach", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_attach_note_shows_under_the_Attach_button_in_either_size(bool compact)
    {
        var (text, name, below) = await OnUi(() =>
        {
            var (window, button, _) = Show(Ready, compact);
            var note = Named<TextBlock>(button, "AttachNote");
            var primary = Named<Button>(button, "AttachPrimary");
            var result = (note.Text, AutomationProperties.GetName(note), note.TranslatePoint(default, window)!.Value.Y >= primary.TranslatePoint(new Point(0, primary.Bounds.Height), window)!.Value.Y - 0.5);
            window.Close();
            return result;
        });

        Assert.Equal(Note, text);
        Assert.Equal(Note, name);
        Assert.True(below);
    }

    [Fact]
    public async Task The_buttons_are_named_for_what_they_act_on()
    {
        var names = await OnUi(() =>
        {
            var (window, button, _) = Show(Ready);
            var result = (
                AutomationProperties.GetName(Named<Button>(button, "AttachPrimary")),
                AutomationProperties.GetName(Named<Button>(button, "AttachChevron")));
            window.Close();
            return result;
        });

        Assert.Equal($"Attach to {Address}", names.Item1);
        Assert.Equal($"More actions for {Address}", names.Item2);
    }

    [Fact]
    public async Task Pending_shows_a_dashed_chip_and_no_live_looking_button()
    {
        var (primaryShown, chipText, dashed, noteShown) = await OnUi(() =>
        {
            var (window, button, _) = Show(new AttachInput(Address, AttachStepKind.Agent, AttachReady: false));
            var chip = Named<Control>(button, "PendingChip");
            var result = (
                button.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "AttachPrimary" && b.IsEffectivelyVisible),
                chip.GetVisualDescendants().OfType<TextBlock>().First().Text,
                chip.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().Any(r => r.StrokeDashArray is { Count: > 0 }),
                Named<TextBlock>(button, "AttachNote").IsEffectivelyVisible);
            window.Close();
            return result;
        });

        Assert.False(primaryShown);
        Assert.Equal("Available with API P10a", chipText);
        Assert.True(dashed);
        Assert.False(noteShown);
    }

    [Fact]
    public async Task The_chevron_opens_from_the_keyboard_and_Esc_returns_focus_to_it()
    {
        var (opened, closed, focused) = await OnUi(() =>
        {
            var (window, button, _) = Show(Ready);
            var chevron = Named<Button>(button, "AttachChevron");
            _ = chevron.Focus(NavigationMethod.Tab);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
            Settle(window);
            var open = chevron.Flyout?.IsOpen == true;
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
            Settle(window);
            var result = (open, chevron.Flyout?.IsOpen == false, chevron.IsFocused);
            window.Close();
            return result;
        });

        Assert.True(opened);
        Assert.True(closed);
        Assert.True(focused);
    }

    [Fact]
    public async Task Cancel_shows_the_confirm_line_with_Keep_focused()
    {
        var (line, keepFocused, keepName) = await OnUi(() =>
        {
            var (window, button, vm) = Show(Ready);
            vm.Choose(AttachAction.Cancel);
            Settle(window);
            var keep = Named<Button>(button, "KeepButton");
            var result = (Named<TextBlock>(button, "ConfirmLine").Text, keep.IsFocused, AutomationProperties.GetName(keep));
            window.Close();
            return result;
        });

        Assert.Equal($"Cancel {Address}?", line);
        Assert.True(keepFocused);
        Assert.Equal($"Keep {Address}", keepName);
    }
}
