using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>The facts panel (screens §4.2, U8 T5): every input and output the recipe declares, in served order, each field a served one.</summary>
[Lane(TestLane.Unit)]
public sealed class FactsPanelViewModelTests
{
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private FactsPanelViewModel Panel(string scenario)
    {
        var detail = FixtureInstances.Detail(scenario);
        return new FactsPanelViewModel(detail, FactViewerRegistry.CreateDefault(), new FactViewerContext(_server, _clipboard, _clock, () => FactFixtures.Api, detail.Id, _ => { }));
    }

    private FactRowViewModel Row(string scenario, string key) => Panel(scenario).Rows.Single(r => r.Key == key);

    [Fact]
    public void Rows_keep_served_order()
    {
        Assert.All(FixtureInstances.Details.Where(d => d.Detail.Facts.Count > 0), one =>
            Assert.Equal(one.Detail.Facts.Select(f => f.Key), Panel(one.Scenario).Rows.Select(r => r.Key), StringComparer.Ordinal));
        Assert.NotEmpty(Panel("project-build").Rows);
    }

    [Fact]
    public void Status_maps_to_produced_in_progress_pending_glyphs()
    {
        var facts = FixtureInstances.Detail("project-build").Facts.Concat(FixtureInstances.Detail("incident-loop").Facts).ToList();
        var glyphs = new Dictionary<FactStatus, string> { [FactStatus.Produced] = Labels.MarkDone, [FactStatus.InProgress] = Labels.MarkActive, [FactStatus.Pending] = Labels.MarkPending };

        Assert.Equal([FactStatus.Produced, FactStatus.InProgress, FactStatus.Pending], facts.Select(f => f.Status).Distinct().Order());
        foreach (var fact in facts)
        {
            var scenario = FixtureInstances.Detail("project-build").Facts.Any(f => f.Key == fact.Key && f.Status == fact.Status) ? "project-build" : "incident-loop";
            Assert.Equal(glyphs[fact.Status], Row(scenario, fact.Key).Glyph);
        }
    }

    [Fact]
    public void The_panel_leads_with_what_this_run_collected_and_says_why_it_is_there()
    {
        var panel = Panel("project-build");

        Assert.Equal(("What this run collected", "Each stage adds what it found; later stages and agents read it."), (panel.Heading, panel.Hint));
    }

    [Fact]
    public void The_panel_is_closed_until_an_output_waits_on_the_person_and_keeps_the_choice()
    {
        var detail = FixtureInstances.Details.First(d => d.Detail.Facts.Any(f => f.Status == FactStatus.Pending && f.Pending is not null)).Detail;
        var context = new FactViewerContext(_server, _clipboard, _clock, () => FactFixtures.Api, detail.Id, _ => { });
        var waiting = new FactsPanelViewModel(detail, FactViewerRegistry.CreateDefault(), context);
        var quiet = Panel("done");

        Assert.Equal((true, false, false), (waiting.IsOpen, quiet.Rows.Any(r => r.IsAwaiting), quiet.IsOpen));
        quiet.IsOpen = true;
        Assert.True(quiet.IsOpen);
    }

    [Fact]
    public void By_human_reads_you()
    {
        var human = FixtureInstances.Detail("project-build").Facts.First(f => f.By?.StartsWith("human:", StringComparison.Ordinal) == true);

        Assert.Equal(Labels.ByYou, Row("project-build", human.Key).By);
        Assert.Equal(string.Empty, Row("project-build", "fact:evidence").By);
    }

    [Fact]
    public void By_other_actors_render_verbatim()
    {
        var others = FixtureInstances.Detail("done").Facts.Where(f => f.By is not null && !f.By.StartsWith("human:", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(others);
        Assert.All(others, f => Assert.Equal(f.By, Row("done", f.Key).By));
    }

    [Fact]
    public void At_reads_the_local_time()
    {
        var at = FixtureInstances.Detail("project-build").Facts.First(f => f.At is not null).At!.Value;
        _clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus two", "plus two"));

        var row = Row("project-build", FixtureInstances.Detail("project-build").Facts.First(f => f.At is not null).Key);

        Assert.Contains(at.AddHours(2).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), row.At, StringComparison.Ordinal);
        Assert.Equal(string.Empty, Row("project-build", "fact:evidence").At);
    }

    [Fact]
    public void Progress_reads_n_of_m()
    {
        var progress = FixtureInstances.Detail("project-build").Facts.Single(f => f.Progress is not null).Progress!;

        Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{progress.Done} of {progress.Total}"), Row("project-build", "fact:evidence").ProgressText);
        Assert.Equal(string.Empty, Row("project-build", "fact:prd").ProgressText);
    }

    [Fact]
    public void From_places_the_band_mark_on_its_state()
    {
        var detail = FixtureInstances.Detail("project-build");
        var outputs = detail.Facts.Where(f => f.From is not null).ToList();

        Assert.NotEmpty(outputs);
        foreach (var fact in outputs)
        {
            var cell = detail.States.Single(s => s.State == fact.From);
            var row = Row("project-build", fact.Key);
            Assert.Contains(fact.From!, row.From, StringComparison.Ordinal);
            Assert.Equal(new StageCellViewModel(cell, isCurrent: false, isBlocked: false).Glyph, row.FromMark);
        }

        Assert.Equal((string.Empty, string.Empty), (Row("project-build", "fact:prd").From, Row("project-build", "fact:prd").FromMark));
    }

    [Fact]
    public async Task Pending_desk_output_shows_awaiting_and_the_cli()
    {
        var detail = FixtureInstances.Detail("dev-desk-round-trip");
        var pending = detail.Facts.Single(f => f.Pending is not null);
        var complete = detail.Current.Status.Commands.Single(c => c.Verb == StatusVerb.Complete);

        var row = Row("dev-desk-round-trip", pending.Key);
        await row.CopyCli(Token);

        Assert.Equal((true, pending.Summary, complete.Cli, Labels.AvailableWithU8b), (row.IsAwaiting, row.Summary, row.PendingCli, row.PendingChip));
        Assert.Equal((complete.Cli, Labels.Copied), (_clipboard.Text, row.CopyingCli.Label));
        Assert.False(row.CanView);
        Assert.False(Row("dev-desk-round-trip", "fact:request").IsAwaiting);
    }
}
