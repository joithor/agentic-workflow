using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>U8 key frames (UI spec §18.2: one per state, integration lane) for the Workflows page, against the in-memory API serving the list goldens. Saved under frames/ as u8-workflows-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsFramesTests : FrameHost
{
    private const string Prefix = "";

    private async Task<(Window Window, ShellViewModel Shell)> OnWorkflows(Func<ShellViewModel, bool> ready)
    {
        var (window, shell) = await Connected(ready);
        shell.GoTo(PageId.Workflows);
        Settle(window);
        return (window, shell);
    }

    private static List<Border> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "WorkflowRowFrame" && b.IsEffectivelyVisible)];

    private static string Address(Border frame) => ((InstanceRowViewModel)frame.DataContext!).Address;

    [Fact]
    public Task u8_workflows_open() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(s => s.Workflows.Groups.Count > 0);

        Expect.Sequence(shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address), Rows(window).Select(Address));
        Assert.Equal(shell.Workflows.Head, Named<TextBlock>(window, "WorkflowsHead").Text);
        var locks = window.GetVisualDescendants().OfType<Ui.Views.Components.ProtectedActionButton>().Where(b => b.Name == "WorkflowProtected" && b.IsEffectivelyVisible).ToList();
        var locked = FixtureInstances.List("open").Instances.Single(r => r.Answer is not null && string.Equals(r.Answer.Class, "protected", StringComparison.Ordinal));
        Assert.Equal(locked.Answer!.Cli, ((ViewModels.Components.ProtectedActionViewModel)Assert.Single(locks).DataContext!).Cli);   // the one lock sits on the Locked row
        Assert.Equal((true, shell.Workflows.LegendText), (Visible(window, "WorkflowsLegend"), Named<TextBlock>(window, "WorkflowsLegend").Text));
        Assert.Equal(shell.Workflows.Groups.Count, window.GetVisualDescendants().OfType<Button>().Count(t => t.Name == "WorkflowGroupToggle"));
        AssistiveNames(window, shell);
        Frame(window, "u8-workflows-open", Prefix);
    });

    // What assistive tech hears, from each control's automation peer (a TextBlock's peer ignores a Name, so these sit on wrappers): review run 2, W5 and W6.
    private static void AssistiveNames(Window window, ShellViewModel shell)
    {
        static string Heard(Control control) => Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(control).GetName();
        List<Border> Wrappers(string name) => [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == name && b.IsEffectivelyVisible)];

        Assert.Equal(WorkflowsLegend.Name, Heard(Assert.Single(Wrappers("WorkflowsLegendName"))));
        var current = Wrappers("WorkflowStageCell").First(b => ((StageCellViewModel)b.DataContext!).IsCurrent);
        Assert.Equal(((StageCellViewModel)current.DataContext!).Name, Heard(current));
        Assert.Contains(Labels.StageCurrent, Heard(current), StringComparison.Ordinal);
        var input = Wrappers("WorkflowInputCell")[0];
        Assert.Equal(((InputCellViewModel)input.DataContext!).Name, Heard(input));
        var row = shell.Workflows.Groups[0].Rows[0];
        var title = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "WorkflowRowOpen" && b.IsEffectivelyVisible && ReferenceEquals(b.DataContext, row));
        Assert.Equal(Labels.OpenInstanceName(row.Subject, row.Address), Heard(title));
        Assert.Contains(row.Subject, Heard(title), StringComparison.Ordinal);
    }

    [Fact]
    public Task u8_workflows_recipe_filter() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(s => s.Workflows.Groups.Count > 0);
        var recipe = (string)GoldenFiles.Node("workflow-instances", "recipe-filter")["instances"]![0]!["recipe"]!["id"]!;

        await shell.Workflows.FilterRecipe(recipe, Token);
        await Until(window, () => Rows(window).Count > 0 && Rows(window).TrueForAll(b => ((InstanceRowViewModel)b.DataContext!).Recipe == recipe));

        Assert.Equal(shell.Workflows.Head, Named<TextBlock>(window, "WorkflowsHead").Text);
        Frame(window, "u8-workflows-recipe-filter", Prefix);
    });

    [Fact]
    public Task u8_workflows_empty() => OnUi(async () =>
    {
        Server.WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty");
        var (window, shell) = await OnWorkflows(s => s.Workflows.IsEmpty);

        Assert.Equal((true, (string)GoldenFiles.Node("workflow-instances", "empty")["empty"]!), (Visible(window, "WorkflowsPageEmpty"), Named<TextBlock>(window, "WorkflowsPageEmpty").Text));
        Assert.Empty(Rows(window));
        Assert.False(Visible(window, "WorkflowsDoneToggle"));
        Frame(window, "u8-workflows-empty", Prefix);
    });

    [Fact]
    public Task u8_workflows_stalled() => OnUi(async () =>
    {
        Server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        var (window, shell) = await OnWorkflows(s => s.Health.Banner.IsStalled && s.Workflows.Groups.Count > 0);

        Assert.True(Visible(window, "WorkflowsPageStale"));
        Assert.NotEmpty(Rows(window));
        Assert.All(shell.Workflows.Groups.SelectMany(g => g.Rows), r => Assert.True(r.IsStale));
        Frame(window, "u8-workflows-stalled", Prefix);
    });

    [Fact]
    public Task u8_workflows_unreadable() => OnUi(async () =>
    {
        var body = (JsonObject)GoldenFiles.Node("workflow-instances", "open");
        _ = body.Remove("counts");
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        var (window, _) = await OnWorkflows(s => s.Workflows.IsUnreadable);

        Assert.Equal((true, Labels.StripUnreadable), (Visible(window, "WorkflowsPageUnreadable"), Named<TextBlock>(window, "WorkflowsPageUnreadable").Text));
        Assert.Empty(Rows(window));
        Frame(window, "u8-workflows-unreadable", Prefix);
    });
}
