using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The frame's tab strip (screens §4.2, U8 T7): the counts the instance serves, a missing context count hiding its number, and each tab naming the phase that makes it live.</summary>
[Lane(TestLane.Unit)]
public sealed class FrameTabsViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static JsonNode Served => GoldenFiles.Node("workflow-instance", "project-build");

    private static ViewCounts Counts => FixtureInstances.Details.First(d => string.Equals(d.Scenario, "project-build", StringComparison.Ordinal)).Detail.Counts;

    private static string Figure(string name) => Served["counts"]![name]!.ToJsonString();

    [Fact]
    public void Counts_come_from_counts()
    {
        var tabs = new FrameTabsViewModel(Counts).Tabs;

        Expect.Sequence([$"Timeline {Figure("timeline")}", $"Decisions {Figure("decisions")}", $"Requests {Figure("requests")}"], tabs.Take(3).Select(t => t.Text));
        Assert.Equal(["41", "3", "1"], [Figure("timeline"), Figure("decisions"), Figure("requests")]);
    }

    [Fact]
    public void Null_context_count_hides_the_number()
    {
        Assert.Null(Served["counts"]!["context"]);

        var context = new FrameTabsViewModel(Counts).Tabs[3];

        Assert.Equal(("Context", "Context, " + Labels.TabArrivesU9), (context.Text, context.Name));
    }

    [Fact]
    public void A_served_context_count_shows()
    {
        var context = new FrameTabsViewModel(Counts with { Context = 14 }).Tabs[3];

        Assert.Equal("Context 14", context.Text);
    }

    [Fact]
    public void Tabs_say_arrive_with_their_phase()
    {
        var tabs = new FrameTabsViewModel(Counts).Tabs;

        Expect.Sequence([Labels.TabsArriveU8b, Labels.TabsArriveU8b, Labels.TabsArriveU8b, Labels.TabArrivesU9], tabs.Select(t => t.Phase));
        Assert.All(tabs, t => Assert.EndsWith(t.Phase, t.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_frame_holds_the_strip_after_a_read_and_keeps_it_when_nothing_changed()
    {
        var frame = new InstanceFrameViewModel(new FakeSindriApi(), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        Assert.Null(frame.Tabs);

        await frame.Open(_api, (string)Served["address"]!, (string)Served["id"]!, TestContext.Current.CancellationToken);
        var strip = frame.Tabs;
        await frame.Refresh(_api, TestContext.Current.CancellationToken);

        Assert.Equal(("Timeline " + Figure("timeline"), true), (strip?.Tabs[0].Text, ReferenceEquals(strip, frame.Tabs)));
    }
}
