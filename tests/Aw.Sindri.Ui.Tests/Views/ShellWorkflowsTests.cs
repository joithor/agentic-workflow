using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The Workflows page in the window (U8 T2): G W lands on it, the tab counts the open figure, events re-read it, and a re-read never moves what the human holds.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ShellWorkflowsTests : FrameHost
{
    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Border> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "WorkflowRowFrame" && b.IsEffectivelyVisible)];

    [Fact]
    public Task G_W_shows_the_Workflows_page_not_the_placeholder() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0);

        Press(window, Key.G);
        Press(window, Key.W);
        Settle(window);

        Assert.Equal(PageId.Workflows, shell.CurrentPage);
        Assert.Equal(("WorkflowsPage", true), (Named<Control>(window, "WorkflowsPage").GetType().Name, Visible(window, "WorkflowsPage")));
        Expect.Sequence(shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address), Rows(window).Select(b => ((InstanceRowViewModel)b.DataContext!).Address));
    });

    [Fact]
    public Task Workflows_nav_count_is_counts_open() => OnUi(async () =>
    {
        var (_, shell) = await Connected(s => s.Workflows.NavCount.Length > 0);

        var open = GoldenFiles.Node("workflow-instances", "open")["counts"]!["open"]!.ToJsonString();
        var entry = shell.Navigation.First(n => n.Page == PageId.Workflows);
        Assert.Equal((open, true), (entry.Count, entry.HasCount));
    });

    [Fact]
    public Task Workflow_events_reread_the_list() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0);
        shell.GoTo(PageId.Workflows);
        var (calls, reads) = (Server.InstancesCalls, shell.Health.ViewReads);

        Server.Push(A.Event(80, "workflow.started"));

        await Until(window, () => shell.Health.ViewReads > reads && Server.InstancesCalls >= calls + 2, "the list was not read again");
        Assert.True(Server.LastInstancesQuery is { Root: true });
    });

    [Fact]
    public Task Refresh_never_moves_focus_scroll_or_input() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0);
        shell.GoTo(PageId.Workflows);
        Settle(window);
        var open = Named<Button>(window, "WorkflowRowOpen");
        Assert.True(open.Focus(NavigationMethod.Tab));
        var filter = Named<TextBox>(window, "SubjectFilter");
        filter.Text = "typed, not yet applied";
        var scroll = Named<ScrollViewer>(window, "Pages");
        var (focused, offset, reads) = (window.FocusManager?.GetFocusedElement(), scroll.Offset, shell.Health.ViewReads);

        Server.Push(A.Event(81, "workflow.started"));
        await Until(window, () => shell.Health.ViewReads > reads, "the views were not read again");

        Assert.Same(focused, window.FocusManager?.GetFocusedElement());
        Assert.Equal(("typed, not yet applied", offset), (Named<TextBox>(window, "SubjectFilter").Text, scroll.Offset));
    });
}
