using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Where focus is while the instance frame opens, stacks and closes (review run 1, V3 to V5; UI spec §11.2): the initial control is real, Enter again goes in, Back and Esc go back to where the user was.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ShellInstanceFocusTests : FrameHost
{
    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Button? FocusedButton(Window window) => window.FocusManager?.GetFocusedElement() as Button;

    private static bool OnNamed(Window window, string name) => string.Equals(FocusedButton(window)?.Name, name, StringComparison.Ordinal);

    private static bool OnChildEntry(Window window, string address) =>
        FocusedButton(window) is { DataContext: ChildRowViewModel row } button && string.Equals(button.Name, "ChildRowOpen", StringComparison.Ordinal) && string.Equals(row.Address, address, StringComparison.Ordinal);

    private static bool OnWorkflowRow(Window window, string address) =>
        FocusedButton(window) is { DataContext: InstanceRowViewModel row } button && string.Equals(button.Name, "WorkflowRowOpen", StringComparison.Ordinal) && string.Equals(row.Address, address, StringComparison.Ordinal);

    private static List<Button> Buttons(Window window, string name) =>
        [.. window.GetVisualDescendants().OfType<Button>().Where(b => string.Equals(b.Name, name, StringComparison.Ordinal) && b.IsEffectivelyVisible)];

    private async Task<(Window Window, ShellViewModel Shell, string Address)> OpenedFromTheWorkflowsRow(string scenario, double width = 1600)
    {
        var address = FixtureInstances.Detail(scenario).Address;
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0, width: width, height: 1000);
        shell.GoTo(PageId.Workflows);
        Settle(window);
        Assert.True(shell.Workflows.Select(address));
        Press(window, Key.Enter);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown && string.Equals(shell.Instance.Address, address, StringComparison.Ordinal));
        return (window, shell, address);
    }

    [Fact]
    public Task Enter_again_on_the_row_whose_frame_is_open_moves_into_the_frame() => OnUi(async () =>
    {
        var (window, shell, address) = await OpenedFromTheWorkflowsRow("project-build");
        var expanded = shell.Instance.Expanded;
        var row = Buttons(window, "WorkflowRowOpen").First(b => b.DataContext is InstanceRowViewModel r && string.Equals(r.Address, address, StringComparison.Ordinal));
        Assert.True(row.Focus(NavigationMethod.Tab));

        Press(window, Key.Enter);

        await Until(window, () => OnNamed(window, "InstanceOpenIn"), $"focus is on {FocusedButton(window)?.Name}");
        Assert.Same(expanded, shell.Instance.Expanded);

        // a re-read of the list while the control the human is on in the sheet goes away must not pull focus out to the page (§3.3, a11y-r2-6)
        FocusedButton(window)!.IsVisible = false;
        await shell.Workflows.Refresh(shell.Health.ApiUrl()!, Token);
        Settle(window);
        Assert.False(window.FocusManager?.GetFocusedElement() is Avalonia.Visual focused && Named<Control>(window, "Page").IsVisualAncestorOf(focused), "a background re-read moved focus to the page");
    });

    [Fact]
    public Task Back_from_a_child_focuses_the_child_entry_and_Esc_returns_to_the_Workflows_row() => OnUi(async () =>
    {
        var (window, shell, address) = await OpenedFromTheWorkflowsRow("project-build");
        var child = shell.Instance.Children!.Groups.Single().Streams[0].Rows[0];
        Click(Buttons(window, "ChildRowOpen")[0]);
        await Until(window, () => string.Equals(shell.Instance.Address, child.Address, StringComparison.Ordinal) && shell.Instance.Phase != FramePhase.Loading);
        await Until(window, () => FocusedButton(window) is { } held && Named<Control>(window, "InstanceFrame").IsVisualAncestorOf(held), "focus is not in the child's frame");

        Click(Named<Button>(window, "SheetBack"));
        await Until(window, () => string.Equals(shell.Instance.Address, address, StringComparison.Ordinal) && OnChildEntry(window, child.Address), "focus did not return to the child entry");

        Press(window, Key.Escape);
        await Until(window, () => !shell.IsSheetOpen && OnWorkflowRow(window, address), "focus did not return to the Workflows row");
    });

    [Fact]
    public Task A_row_open_where_the_sheet_covers_the_page_stays_on_the_heading() => OnUi(async () =>
    {
        var (window, shell, _) = await OpenedFromTheWorkflowsRow("project-build", width: 1000);

        Settle(window);   // the frame is read: focus must not have moved on from the heading to its first action (§11.2)
        Assert.True(Named<Border>(window, "SheetTitle").IsFocused, $"focus is on {window.FocusManager?.GetFocusedElement()}");
        Assert.True(shell.IsSheetShown);
    });
}
