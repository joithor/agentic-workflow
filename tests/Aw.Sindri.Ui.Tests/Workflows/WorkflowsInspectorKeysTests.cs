using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>F3 workflows spec §3, §5 (Q1): the first Enter selects and opens the drawer, the second opens the run; J / K carry the drawer; Esc closes it.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsInspectorKeysTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(WorkflowsViewModel List, List<OpenRequestedEventArgs> Opened)> Shown()
    {
        var list = new WorkflowsViewModel(new FakeSindriApi(), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await list.Refresh(_api, Token);
        list.SetViewMode(WorkflowViewMode.List);
        var opened = new List<OpenRequestedEventArgs>();
        list.OpenRequested += (_, e) => opened.Add(e);
        return (list, opened);
    }

    [Fact]
    public async Task The_first_Enter_opens_the_drawer_and_the_second_opens_the_run()
    {
        var (list, opened) = await Shown();
        Assert.True(list.Press("J"));
        var address = list.SelectedAddress;

        Assert.True(list.Press("Enter"));
        await list.InspectorRead;
        Assert.Empty(opened);
        Assert.Equal((true, address), (list.IsInspectorOpen, list.Inspector.Address));

        Assert.True(list.Press("Enter"));
        Assert.Equal([address], opened.Select(o => o.Link.Subject), StringComparer.Ordinal);
    }

    [Fact]
    public async Task J_and_K_carry_the_open_drawer_to_the_run_they_select()
    {
        var (list, _) = await Shown();
        Assert.True(list.Press("J"));
        Assert.True(list.Press("Enter"));

        Assert.True(list.Press("J"));
        await list.InspectorRead;

        Assert.Equal(list.SelectedAddress, list.Inspector.Address);
        Assert.StartsWith("2 of ", list.Inspector.PositionText, StringComparison.Ordinal);
        Assert.True(list.Press("K"));
        Assert.StartsWith("1 of ", list.Inspector.PositionText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Esc_closes_the_drawer_and_a_second_Esc_is_not_used()
    {
        var (list, _) = await Shown();
        Assert.True(list.Press("J"));
        Assert.True(list.Press("Enter"));

        Assert.True(list.Press("Esc"));
        Assert.False(list.IsInspectorOpen);
        Assert.False(list.Press("Esc"));
    }

    [Fact]
    public async Task Selecting_nothing_closes_the_drawer()
    {
        var (list, _) = await Shown();
        Assert.True(list.Press("J"));
        Assert.True(list.Press("Enter"));

        list.Deselect();

        Assert.False(list.IsInspectorOpen);
    }

    [Fact]
    public async Task A_click_opens_the_drawer_on_the_run()
    {
        var (list, opened) = await Shown();
        var address = list.Groups[0].Rows[0].Address;

        Assert.True(list.OpenInspector(address));
        await list.InspectorRead;

        Assert.Equal((true, address, 0), (list.IsInspectorOpen, list.Inspector.Address, opened.Count));
        Assert.NotEmpty(list.Inspector.Stages);
    }
}
