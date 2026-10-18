using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// "Answer in the child" in the real views, in the headless shell against the in-memory API serving the goldens: the rows of Home and
/// Workflows, the expanded state, following it, and the control's name at the automation peers. Frames are saved as u8-answer-child-*.png
/// when AW_UI_FRAMES=1. The park is the layout golden of the pointer row and the status goldens; nothing is made up in the test.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ChildAnswerFramesTests : FrameHost
{
    private const string Prefix = "u8-answer-child-";
    private const string Detail = "project-build";

    private static JsonNode ListWith(string scenario, bool withChild)
    {
        var list = GoldenFiles.Node("workflow-instances", "root");
        var row = JsonNode.Parse(File.ReadAllText(Path.Combine(GoldenFiles.Root, "Layout", "instance-summary-" + scenario + ".json")))!;
        if (!withChild)
        {
            _ = row["answer"]!.AsObject().Remove("child");
        }

        list["instances"]!.AsArray().Insert(0, row);
        return list;
    }

    private static RouteAnswer Served(JsonNode body) => new(HttpStatusCode.OK, body.ToJsonString());

    private static RouteAnswer DetailWith(string statusScenario)
    {
        var detail = GoldenFiles.Node("workflow-instance", Detail);
        detail["current"]!["status"] = GoldenFiles.Node("workflow-status", statusScenario);
        return Served(detail);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static List<Button> Pointers(Visual within) =>
        [.. within.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "AnswerInChild" && b.IsEffectivelyVisible)];

    private static string Heard(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName();

    [Fact]
    public Task Home_strip_and_the_Workflows_list_show_the_pointer_and_no_lock_or_answer_button() => OnUi(async () =>
    {
        Server.InstancesBody = Served(ListWith("escalated-child", withChild: true));
        var (window, shell) = await Connected(width: 1600, height: 1000, ready: s => s.Strip.Rows.Count > 0);
        var served = FixtureInstances.Summary("escalated-child");
        Frame(window, "home-strip", Prefix);

        var strip = Pointers(window);
        var row = strip[0].GetVisualAncestors().OfType<Border>().First(b => b.Name == "StripRowFrame");

        Assert.Equal(Labels.AnswerInChild, strip[0].Content);
        Assert.StartsWith(Labels.AnswerInChild, Heard(strip[0]), StringComparison.Ordinal);
        Assert.Equal(Labels.AnswerInChildName(served.Answer!.Child!), Heard(strip[0]));
        var shown = row.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Name ?? "").ToList();
        Assert.DoesNotContain("LockButton", shown, StringComparer.Ordinal);
        Assert.DoesNotContain("StripAnswer", shown, StringComparer.Ordinal);

        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Groups.Count > 0 && Pointers(window).Count > 0);
        Frame(window, "workflows-list", Prefix);
        var listed = Pointers(window)[0];
        Assert.StartsWith(Labels.AnswerInChild, Heard(listed), StringComparison.Ordinal);
        Assert.DoesNotContain("LockButton", listed.GetVisualAncestors().OfType<Border>().First(b => b.Name == "WorkflowRowFrame").GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Name ?? ""), StringComparer.Ordinal);
    });

    [Fact]
    public Task Following_it_from_a_row_with_a_child_opens_the_childs_frame() => OnUi(async () =>
    {
        Server.InstancesBody = Served(ListWith("escalated-child", withChild: true));
        var (window, shell) = await Connected(width: 1600, height: 1000, ready: s => s.Strip.Rows.Count > 0);
        var served = FixtureInstances.Summary("escalated-child");

        Click(Pointers(window)[0]);

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Answer!.Child), shell.SheetLink);
        Frame(window, "strip-follows-to-child", Prefix);

    });

    [Fact]
    public Task Following_it_from_a_row_without_a_child_opens_the_parents_frame() => OnUi(async () =>
    {
        Server.InstancesBody = Served(ListWith("escalated-child", withChild: false));
        var (window, shell) = await Connected(width: 1600, height: 1000, ready: s => s.Strip.Rows.Count > 0);
        var served = FixtureInstances.Summary("escalated-child");

        Click(Pointers(window)[0]);

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Address), shell.SheetLink);
        Frame(window, "strip-follows-to-parent", Prefix);
    });

    [Fact]
    public Task The_expanded_state_line_follows_the_served_child_by_its_id() => OnUi(async () =>
    {
        Server.InstanceBody = id => string.Equals(id, FixtureInstances.Detail(Detail).Id, StringComparison.Ordinal) ? DetailWith("escalated-child") : null;
        var (window, shell) = await Connected(width: 1600, height: 1000);
        var detail = FixtureInstances.Detail(Detail);
        var status = FixtureInstances.Status("escalated-child");

        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown && Visible(window, "ExpandedAnswerInChild"));
        Frame(window, "expanded-with-child", Prefix);
        var line = Named<Button>(window, "ExpandedAnswerInChild");
        Assert.StartsWith(Labels.AnswerInChild, Heard(line), StringComparison.Ordinal);

        Click(Named<Button>(window, "ExpandedAnswerInChild"));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, status.Child!.Address), shell.SheetLink);
    });

    [Fact]
    public Task The_expanded_state_line_without_a_child_puts_focus_in_the_Children_panel() => OnUi(async () =>
    {
        Server.InstanceBody = id => string.Equals(id, FixtureInstances.Detail(Detail).Id, StringComparison.Ordinal) ? DetailWith("mirrored-budget-no-child") : null;
        var (window, shell) = await Connected(width: 1600, height: 1000);
        var detail = FixtureInstances.Detail(Detail);
        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown && Visible(window, "ExpandedAnswerInChild"));
        Frame(window, "expanded-no-child", Prefix);

        Click(Named<Button>(window, "ExpandedAnswerInChild"));

        await Until(window, () => string.Equals((window.FocusManager?.GetFocusedElement() as Button)?.Name, "ChildRowOpen", StringComparison.Ordinal), "focus did not reach the Children panel");
        Assert.Equal(detail.Address, shell.Instance.Address);
        Frame(window, "expanded-no-child-focus", Prefix);
    });
}
