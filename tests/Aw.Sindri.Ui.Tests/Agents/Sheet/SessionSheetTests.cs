using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Agents.Sheet;

/// <summary>The session sheet on screen (U11a T4): its name and fields as assistive tech hears them, Esc and the edge tab, the pending Focus and the collapsed preview host. Frames: u11a-sheet-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SessionSheetTests : FrameHost
{
    private const string Claude = "claude-code/aaaaaaaa-0000-4000-8000-000000000001";
    private const string Codex = "codex/bbbbbbbb-0000-4000-8000-000000000002";

    // A stand-in for the page's row until the page's own rows exist: a focusable control whose data is a session ref.
    private sealed class StandInRow(string reference) : ISessionRef
    {
        public string Ref { get; } = reference;
    }

    private static void Frame(Window window, string name) => Frame(window, name, "u11a-sheet-");

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static DeepLink Link(string reference) => new(DeepLinkKind.Session, reference);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Opened(string reference, double width = 1600)
    {
        var (window, shell) = await Connected(width: width, height: 1000);
        shell.GoTo(PageId.Agents);
        Settle(window);
        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(Link(reference)));
        await Until(window, () => shell.IsSheetShown && shell.AgentSheet.Phase is FramePhase.Shown or FramePhase.NotFound, "the sheet never showed its answer");
        return (window, shell);
    }

    private static List<(string Name, AutomationControlType Type)> Tree(Window window) => Heard.Tree(Named<Control>(window, "SessionSheet"));

    [Fact]
    public Task Enter_on_a_row_opens_the_session_sheet_named_session_x() => OnUi(async () =>
    {
        var (window, shell) = await Opened(Claude);

        Assert.Equal("Session " + Claude, shell.SheetName);
        Assert.Equal(shell.SheetName, Heard.Name(Named<Control>(window, "Sheet")));
        Assert.True(Visible(window, "SessionSheet"));
        Assert.False(Visible(window, "SheetBody"), "the placeholder is gone");
        Frame(window, "claude-tmux");
    });

    [Fact]
    public Task Every_field_is_heard_as_one_named_text_through_the_peer_tree() => OnUi(async () =>
    {
        var (window, shell) = await Opened(Claude);
        var sheet = shell.AgentSheet;
        var heard = Tree(window);

        var expected = new[] { sheet.Heading, sheet.StateLine, sheet.FocusLine }
            .Concat(sheet.Fields.Select(f => f.Name)).Concat(sheet.HostFields.Select(f => f.Name)).Concat(sheet.History.Select(h => h.Name));
        Assert.All(expected, name => Assert.Contains((name, AutomationControlType.Text), heard));
        Assert.All(heard.Where(h => h.Type == AutomationControlType.Text), h => Assert.False(string.IsNullOrWhiteSpace(h.Name), "a nameless text node in the sheet"));
        Assert.Contains(heard, h => h.Type == AutomationControlType.Button && h.Name == "Copy: Focus");
        Assert.Contains(heard, h => h.Type == AutomationControlType.Button && h.Name.StartsWith("Focus", StringComparison.Ordinal));
    });

    [Fact]
    public Task Focus_is_disabled_and_its_line_and_Copy_are_there() => OnUi(async () =>
    {
        var (window, shell) = await Opened(Codex);

        var focus = Named<Button>(window, "SessionFocus");
        Assert.False(focus.IsEnabled);
        Assert.Equal("Focus, arrives in CO-4a", Heard.Name(focus));
        Assert.Equal(shell.AgentSheet.FocusLine, Heard.Wrapper(Named<Border>(window, "SessionFocusLine")));
        var copy = Named<Button>(window, "SessionFocusCopy");
        Assert.True(copy.Focusable && copy.IsEffectivelyEnabled);
        copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.AgentSheet.CopyLabel == "Copied");
        Assert.Equal("Copied", Heard.Name(copy));
        Frame(window, "codex");
    });

    [Fact]
    public Task Live_preview_host_is_in_the_tree_and_collapsed() => OnUi(async () =>
    {
        var (window, _) = await Opened(Claude);

        var host = Named<ContentControl>(window, "LivePreviewHost");
        Assert.False(host.IsVisible);
        Assert.Null(host.Content);
        Assert.DoesNotContain(host.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible);
    });

    [Fact]
    public Task A_404_says_the_session_has_ended_and_the_sheet_stays_open() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Problem("sessions-id", "not-found");
        var (window, shell) = await Opened(Claude);

        var ended = Named<Border>(window, "SessionEnded");
        Assert.Equal("This session has ended.", Heard.Wrapper(ended));
        Assert.Equal(shell.AgentSheet.EndedDetail, Avalonia.Controls.ToolTip.GetTip(ended));
        Assert.True(shell.IsSheetShown);
        Assert.False(Named<Button>(window, "SessionFocus").IsEnabled);
        Frame(window, "ended");
    });

    [Fact]
    public Task Esc_returns_focus_to_the_opening_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1000, height: 1000);   // narrow: the sheet covers the page and takes focus
        shell.GoTo(PageId.Agents);
        Settle(window);
        var row = new Button { Name = "StandInRow", DataContext = new StandInRow(Claude), Content = "row" };
        Named<Control>(window, "AgentsPage").FindDescendantOfType<Panel>()!.Children.Add(row);
        Settle(window);
        Assert.True(row.Focus(NavigationMethod.Tab));

        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(Link(Claude)));
        await Until(window, () => shell.IsSheetShown && Named<Border>(window, "SheetTitle").IsFocused, "focus never reached the sheet's heading");
        Press(window, Key.Escape);

        await Until(window, () => !shell.IsSheetOpen && ReferenceEquals(window.FocusManager?.GetFocusedElement(), row), $"focus is on {window.FocusManager?.GetFocusedElement()}");
    });

    [Fact]
    public Task Edge_tab_reopens_the_session_sheet() => OnUi(async () =>
    {
        var (window, shell) = await Opened(Claude);
        Press(window, Key.Escape);
        await Until(window, () => !shell.IsSheetOpen);
        Assert.Equal("Reopen session " + Claude, Heard.Name(Named<Button>(window, "EdgeTab")));

        Named<Button>(window, "EdgeTab").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await Until(window, () => shell.IsSheetShown && Visible(window, "SessionSheet"));
        Assert.Equal(Claude, shell.AgentSheet.Ref);
    });

    [Fact]
    public Task Deep_link_session_provider_id_opens_agents_and_the_sheet() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1600, height: 1000);

        shell.Navigate(Link(Codex));
        await Until(window, () => shell.AgentSheet.Phase == FramePhase.Shown);

        Assert.Equal((PageId.Agents, true), (shell.CurrentPage, Visible(window, "SessionSheet")));
        Assert.Equal(shell.AgentSheet.Heading, Heard.Wrapper(Named<Border>(window, "SessionHeading")));
    });
}
