using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>The facts panel inside the instance frame (U8 T5): the frame builds it from the instance it reads, keeps its open viewers across a re-read, and passes an item's Open on.</summary>
[Lane(TestLane.Unit)]
public sealed class FrameFactsTests
{
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<InstanceFrameViewModel> Opened(string scenario)
    {
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);
        var detail = FixtureInstances.Detail(scenario);
        await frame.Open(FactFixtures.Api, detail.Address, detail.Id, Token);
        return frame;
    }

    [Fact]
    public async Task The_frame_shows_the_facts_of_its_instance()
    {
        var frame = await Opened("project-build");

        Assert.Equal(FixtureInstances.Detail("project-build").Facts.Select(f => f.Key), frame.Facts!.Rows.Select(r => r.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_reread_of_the_same_facts_keeps_the_panel_and_its_open_viewer()
    {
        var frame = await Opened("incident-loop");
        var panel = frame.Facts!;
        panel.Rows.Single(r => r.Key == "fact:alert").ToggleView();

        await frame.Refresh(FactFixtures.Api, Token);

        Assert.Same(panel, frame.Facts);
        Assert.True(panel.Rows.Single(r => r.Key == "fact:alert").IsViewing);
    }

    [Fact]
    public async Task An_item_fact_asks_the_frame_to_open_the_item()
    {
        var frame = await Opened("done");
        var asked = new List<string>();
        frame.ItemRequested += (_, e) => asked.Add(e.ItemId);
        var row = frame.Facts!.Rows.Single(r => r.Key == "fact:item");

        row.ToggleView();
        ((ItemFactViewer)row.Viewer!).Open();

        Assert.Equal([FixtureInstances.Detail("done").Facts.Single(f => f.Key == "fact:item").Ref!.ItemId!], asked);
    }

    [Fact]
    public async Task A_new_instance_clears_the_panel_until_it_is_read()
    {
        var frame = await Opened("project-build");

        frame.Show(FixtureInstances.Detail("incident-loop").Address, id: null);

        Assert.Null(frame.Facts);
    }
}
