using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>Home's states (F3 home spec §3): skeleton rows before the first read, then each panel's empty line, the served sentence first and the spec's own words when none is served.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeStatesTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private NeedsYouViewModel NeedsYou() => new(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _clock);

    private WorkflowsStripViewModel Strip() => new(new FakeClipboard(), _clock);

    private static InstanceList NoneOpen(string? empty) => FixtureInstances.Root with
    {
        Instances = [],
        Counts = FixtureInstances.Root.Counts with { Open = 0 },
        Empty = empty,
    };

    [Fact]
    public void Needs_you_shows_skeleton_rows_and_no_empty_line_before_the_first_read()
    {
        var list = NeedsYou();

        Assert.Equal((true, string.Empty), (list.IsLoading, list.EmptyLine));
    }

    [Fact]
    public void Needs_you_with_nothing_served_says_nothing_needs_you()
    {
        var list = NeedsYou();

        list.Update(new NeedsYouFeed([], string.Empty));

        Assert.Equal((false, Labels.HomeNothingNeedsYou), (list.IsLoading, list.EmptyLine));
    }

    [Fact]
    public void Needs_you_keeps_the_sentence_the_api_served()
    {
        var list = NeedsYou();

        list.Update(new NeedsYouFeed([], "Nothing needs you right now."));

        Assert.Equal("Nothing needs you right now.", list.EmptyLine);
    }

    [Fact]
    public void Running_shows_skeleton_rows_until_the_first_list_and_not_when_the_route_is_missing()
    {
        var waiting = Strip();
        var missing = Strip();
        missing.MarkNotServed();

        Assert.Equal((true, false), (waiting.IsLoading, missing.IsLoading));
    }

    [Fact]
    public void Running_with_nothing_open_and_no_sentence_says_to_start_a_workflow()
    {
        var strip = Strip();

        strip.Update(NoneOpen(empty: null));

        Assert.Equal((false, Labels.HomeNothingRunning), (strip.IsLoading, strip.EmptyLine));
    }

    [Fact]
    public void Running_keeps_the_sentence_the_api_served()
    {
        var strip = Strip();

        strip.Update(NoneOpen("Nothing is running."));

        Assert.Equal("Nothing is running.", strip.EmptyLine);
    }

    [Fact]
    public void Running_with_open_rows_has_no_empty_line()
    {
        var strip = Strip();

        strip.Update(FixtureInstances.Root);

        Assert.Equal((false, string.Empty), (strip.IsLoading, strip.EmptyLine));
    }
}
