using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>"Answer in the child" in the rows, read at the automation peers (where AT-SPI reads): a stop in the control view whose name leads with the drawn words.</summary>
[Lane(TestLane.Integration)]
public sealed class ChildAnswerButtonTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ChildAnswerButtonTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (string Name, bool Control, string? Lock) InRow(Control row)
    {
        var window = new Window { Content = row, Width = 1300, Height = 240 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var control = row.GetVisualDescendants().OfType<Button>().First(b => b.Name == "AnswerInChild" && b.IsEffectivelyVisible);
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        var hasLock = row.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "LockButton" && b.IsEffectivelyVisible);
        window.Close();
        return (peer.GetName(), peer.IsControlElement(), hasLock ? "lock" : null);
    }

    [Fact]
    public async Task A_workflows_row_has_the_control_with_its_name_and_no_lock()
    {
        var served = FixtureInstances.Summary("escalated-child");

        var heard = await OnUi(() => InRow(new InstanceRow { DataContext = new InstanceRowViewModel(served, new FakeClipboard(), new FakeTimeProvider(A.Noon)) }));

        Assert.Equal((Labels.AnswerInChildName(served.Answer!.Child!), true, null), heard);
        Assert.StartsWith(Labels.AnswerInChild, heard.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_strip_row_has_the_control_with_its_name_and_no_lock()
    {
        var served = FixtureInstances.Summary("parked-mirrored-budget");

        var heard = await OnUi(() => InRow(new StripRow { DataContext = new StripRowViewModel(served, new FakeClipboard(), new FakeTimeProvider(A.Noon)) }));

        Assert.Equal((Labels.AnswerInChildName(served.Answer!.Child!), true, null), heard);
    }
}
