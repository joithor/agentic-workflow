using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Review run 5, r5-1: a Workflows row that declares no inputs leaves no nameless stop for assistive tech (read at the automation peers, which is where AT-SPI reads).</summary>
[Lane(TestLane.Integration)]
public sealed class InstanceRowInputsTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(InstanceRowInputsTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static List<(string Name, AutomationControlType Type)> Heard_(Ui.Api.Workflow.InstanceSummary served)
    {
        var row = new InstanceRow { DataContext = new InstanceRowViewModel(served, new FakeClipboard(), new FakeTimeProvider(A.Noon)) };
        var window = new Window { Content = row, Width = 1300, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var frame = row.GetVisualDescendants().OfType<Border>().First(b => b.Name == "WorkflowRowFrame");
        var reached = new List<(string Name, AutomationControlType Type)>();
        void Walk(AutomationPeer peer)
        {
            foreach (var child in peer.GetChildren())
            {
                if (child.IsControlElement())   // the control view is what AT-SPI lists as stops; raw peers are walked through but not heard
                {
                    reached.Add((child.GetName(), child.GetAutomationControlType()));
                }

                Walk(child);
            }
        }

        Walk(ControlAutomationPeer.CreatePeerForElement(frame));
        window.Close();
        return reached;
    }

    [Fact]
    public async Task A_row_with_no_inputs_has_no_nameless_stop_and_one_with_inputs_names_them()
    {
        var withInputs = FixtureInstances.List("open").Instances.First(r => r.Inputs.Count > 0);

        var empty = await OnUi(() => Heard_(withInputs with { Inputs = [] }));
        var named = await OnUi(() => Heard_(withInputs));

        Assert.DoesNotContain(empty, r => string.IsNullOrEmpty(r.Name));
        Assert.Contains(named, r => r.Type == AutomationControlType.Text && r.Name.Length > 0 && r.Name.Contains(withInputs.Inputs[0].Label, StringComparison.Ordinal));
    }

    [Fact]
    public async Task One_row_control_reused_from_no_inputs_to_inputs_has_its_wrapper_back_in_the_control_view()
    {
        var withInputs = FixtureInstances.List("open").Instances.First(r => r.Inputs.Count > 0);

        var reached = await OnUi(() =>
        {
            var row = new InstanceRow { DataContext = new InstanceRowViewModel(withInputs with { Inputs = [] }, new FakeClipboard(), new FakeTimeProvider(A.Noon)) };
            var window = new Window { Content = row, Width = 1300, Height = 200 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            row.DataContext = new InstanceRowViewModel(withInputs, new FakeClipboard(), new FakeTimeProvider(A.Noon));
            Dispatcher.UIThread.RunJobs();
            var inputs = row.GetVisualDescendants().OfType<Border>().First(b => b.Name == "WorkflowRowInputs");
            var peer = ControlAutomationPeer.CreatePeerForElement(inputs);
            window.Close();
            return (Control: peer.IsControlElement(), peer.GetName());
        });

        Assert.True(reached.Control);
        Assert.Contains(withInputs.Inputs[0].Label, reached.Item2, StringComparison.Ordinal);
    }
}
