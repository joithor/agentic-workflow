using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>U8 key frames (UI spec §18.2) for the children panel, and the way a child opens and returns, against the in-memory API serving the instance goldens. Saved under frames/ as u8-frame-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ChildrenFramesTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> Opened(string scenario)
    {
        var detail = FixtureInstances.Detail(scenario);
        var (window, shell) = await Connected(width: 1600, height: 1000);
        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Children is not null && shell.Instance.Address == detail.Address);
        Settle(window);
        return (window, shell);
    }

    private static void Frame(Window window, string name) => Frame(window, name, "u8-frame-");

    private static List<Button> Buttons(Window window, string name) =>
        [.. window.GetVisualDescendants().OfType<Button>().Where(b => b.Name == name && b.IsEffectivelyVisible)];

    private static List<TextBlock> Texts(Window window, string name) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible)];

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task u8_frame_children_workstreams() => OnUi(async () =>
    {
        var (window, _) = await Opened("project-build");
        var served = FixtureInstances.Detail("project-build").Children.Single();

        Assert.Equal(served.Workstreams.Select(w => Labels.StreamTitle(w.Stream, w.Label)), Texts(window, "ChildStreamTitle").Select(t => t.Text), StringComparer.Ordinal);
        Assert.Equal(FixtureInstances.ChildRows(served).Count(), Buttons(window, "ChildRowOpen").Count);
        _ = Assert.Single(Texts(window, "ChildMirrored"));
        Assert.Contains(Texts(window, "ChildGroupHeader"), t => t.Text!.Contains(served.Recipe, StringComparison.Ordinal));
        Named<Control>(window, "ChildrenBlock").BringIntoView();
        Frame(window, "children-workstreams");
    });

    [Fact]
    public Task u8_frame_children_more() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-many-children");

        Assert.Equal(shell.Instance.Children!.Groups.Single().MoreText, Texts(window, "ChildMore").Single().Text);
        _ = Assert.Single(Buttons(window, "ChildOpenAll"));
        Named<Control>(window, "ChildrenBlock").BringIntoView();
        Frame(window, "children-more");
    });

    [Fact]
    public Task A_child_opened_from_the_frame_is_stacked_and_Back_returns_to_its_parent() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-build");
        var parent = shell.Instance.Address;
        var child = shell.Instance.Children!.Groups.Single().Streams[0].Rows[0];

        Click(Buttons(window, "ChildRowOpen")[0]);
        await Until(window, () => shell.Instance.Address == child.Address);
        Assert.Equal((true, child.Id), (shell.IsInstanceSheet, Server.LastInstanceId));

        Click(Named<Button>(window, "SheetBack"));
        await Until(window, () => shell.Instance.Address == parent && shell.Instance.Phase == FramePhase.Shown);

        Assert.True(shell.IsSheetOpen);
        Assert.NotNull(window.FocusManager?.GetFocusedElement());
    });

    [Fact]
    public Task Esc_closes_the_whole_stack_and_leaves_no_trail() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-build");
        Click(Buttons(window, "ChildRowOpen")[0]);
        await Until(window, () => shell.Instance.Address != FixtureInstances.Detail("project-build").Address);

        Press(window, Key.Escape);
        Assert.False(shell.IsSheetOpen);
        shell.Navigate(new DeepLink(DeepLinkKind.Workflow, FixtureInstances.Detail("project-build").Address));
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown);
        shell.Back();

        Assert.False(shell.IsSheetOpen);
    });

    [Fact]
    public Task An_items_Open_opens_its_sheet_stacked_and_Back_returns_to_the_frame() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-build");
        var parent = shell.Instance.Address;
        var row = shell.Instance.Children!.Groups.Single().Streams[0].Rows[0];

        Click(Buttons(window, "ChildRowItemOpen")[0]);
        Settle(window);
        Assert.Equal((DeepLinkKind.Item, row.ItemId), (shell.SheetLink!.Kind, shell.SheetSubject));

        shell.Back();
        await Until(window, () => shell.IsInstanceSheet && shell.Instance.Address == parent && shell.Instance.Phase == FramePhase.Shown);
    });
}
