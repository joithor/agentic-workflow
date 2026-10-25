using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Home Workflows strip (screens §4.1, E-20/E-21): the root rows as served, the cap and the fold, the banners.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsStripViewModelTests
{
    private static readonly string[] _openAddresses = [.. FixtureInstances.Rows.Where(FixtureInstances.IsOpen).Select(r => r.Address)];

    private readonly FakeTimeProvider _clock = new(A.Noon);

    private WorkflowsStripViewModel Strip(InstanceList? list = null)
    {
        var strip = new WorkflowsStripViewModel(new FakeClipboard(), _clock);
        strip.Update(list ?? FixtureInstances.Root);
        return strip;
    }

    private static string[] Addresses(WorkflowsStripViewModel strip) => [.. strip.Rows.Select(r => r.Address)];

    [Fact]
    public void Rows_keep_the_apis_order()
    {
        var strip = Strip();
        strip.SetNeedsYouShown(0);

        Expect.Sequence(_openAddresses.Take(WorkflowsStripViewModel.Cap), Addresses(strip));
        Assert.Equal(RecipeUrgency.Incident, FixtureInstances.Rows[0].Recipe.Urgency);
    }

    [Fact]
    public void Finished_instances_are_not_on_the_strip()
    {
        var strip = Strip();
        strip.SetNeedsYouShown(0);

        Assert.DoesNotContain(strip.Rows, r => FixtureInstances.Rows.Any(f => string.Equals(f.Address, r.Address, StringComparison.Ordinal) && !FixtureInstances.IsOpen(f)));
    }

    [Fact]
    public void Caps_at_five_then_shows_N_more()
    {
        var strip = Strip();
        strip.SetNeedsYouShown(0);

        Assert.Equal((WorkflowsStripViewModel.Cap, true, Labels.MoreRows(_openAddresses.Length - WorkflowsStripViewModel.Cap)), (strip.Rows.Count, strip.HasMore, strip.MoreText));
    }

    // The server decides what is open (the strip sends the page's query); "… N more" reads the served counts.open, not the rows sent.
    [Fact]
    public void N_more_reads_counts_open_not_the_rows_sent()
    {
        var root = FixtureInstances.Root;
        var open = root.Instances.Where(FixtureInstances.IsOpen).Take(2).ToList();
        var strip = Strip(root with { Instances = open, Counts = root.Counts with { Open = 9 } });
        strip.SetNeedsYouShown(0);

        Assert.Equal((open.Count, true, Labels.MoreRows(9 - open.Count)), (strip.Rows.Count, strip.HasMore, strip.MoreText));
    }

    [Fact]
    public void N_more_opens_Workflows()
    {
        var strip = Strip();
        var asked = 0;
        strip.WorkflowsRequested += (_, _) => asked++;

        strip.OpenWorkflows();

        Assert.Equal(1, asked);
    }

    [Fact]
    public void Heading_reads_Workflows_N_open_from_the_served_count()
    {
        var strip = Strip();

        Assert.Equal((Labels.Workflows, InstanceHeads.Strip(FixtureInstances.Counts)), (strip.Heading, strip.OpenText));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(2, 5)]
    [InlineData(0, 5)]
    [InlineData(4, 4)]
    public void Fold_gives_rows_back_to_needs_you_first(int needsYou, int strip)
    {
        var vm = Strip();

        vm.SetNeedsYouShown(needsYou);

        Assert.Equal(strip, vm.Rows.Count);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(2, 2)]
    public void Text_scale_1_3_drops_the_strip_cap_to_two(int needsYou, int strip)
    {
        var vm = Strip();
        vm.SetNeedsYouShown(needsYou);

        vm.TextScale = 1.3;

        Assert.Equal(strip, vm.Rows.Count);
    }

    [Fact]
    public void Empty_strip_shows_the_served_sentence_and_no_N_more()
    {
        var empty = EmptyList();

        var strip = Strip(empty);

        Assert.Equal((true, empty.Empty, false, ""), (strip.IsEmpty, strip.EmptyText, strip.HasMore, strip.MoreText));
        Assert.Empty(strip.Rows);
    }

    private static InstanceList EmptyList() => new([], new ListCounts(Open: 0, NeedsYou: 0, WithYou: 0, Running: 0, Done: 0), "served sentence", A.Noon);

    [Fact]
    public void Stalled_keeps_rows_marked_stale()
    {
        var strip = Strip();

        strip.SetStale(stale: true);

        Assert.All(strip.Rows, r => Assert.True(r.IsStale));
        Assert.NotEmpty(strip.Rows);
    }

    [Fact]
    public void Not_served_yet_says_so_in_plain_words_when_the_route_is_absent()
    {
        var strip = Strip();

        strip.MarkNotServed();

        Assert.Equal((true, Labels.ArrivesWorkflowStrip, 0), (strip.IsNotServed, strip.NotServedText, strip.Rows.Count));
        Assert.Equal("The workflow list isn't available from this Sindri yet.", strip.NotServedText);
    }

    [Fact]
    public void An_update_after_not_served_shows_rows_again()
    {
        var strip = Strip();
        strip.MarkNotServed();

        strip.Update(FixtureInstances.Root);

        Assert.False(strip.IsNotServed);
        Assert.NotEmpty(strip.Rows);
    }
}
