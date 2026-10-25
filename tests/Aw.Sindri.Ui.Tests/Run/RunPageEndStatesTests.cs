using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Microsoft.Extensions.Time.Testing;
using Labels = Aw.Sindri.Ui.ViewModels.Labels;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>The run page's endings (F3 spec 3 §8.5): a failed step shows why from the instance events with a red edge to the end, and a run that did not work out or was cancelled reads its end word with a solid edge.</summary>
[Lane(TestLane.Unit)]
public sealed class RunPageEndStatesTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Body<T>(T value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value, SindriApiClient.Json));

    // The line stopped at plan with its child in the given chip, ended at "stuck" with the given outcome.
    private static InstanceDetail Ended(string planChip, string outcome)
    {
        var line = RunFixtures.Line("plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["plan"] = planChip });
        return line with
        {
            State = "stuck",
            Since = A.Noon.AddHours(-1),
            States = [.. line.States.Select(c => string.Equals(c.State, "stuck", StringComparison.Ordinal) ? c with { Glyph = StateGlyph.Done, Outcome = outcome } : c)],
        };
    }

    private static InstanceDetail FailedPlanChild()
    {
        var child = RunFixtures.Child("plan", ("produce", StateGlyph.Active, false), ("check", StateGlyph.Pending, false));
        var run = new StepRun("review.1", "produce", Attempt: 1, MaxAttempts: null, "failed", "agent", A.Noon.AddHours(-4), A.Noon.AddHours(-3), DurationMs: 3_600_000, Outcome: null, Tokens: null, Actor: null);
        return child with { StepRuns = [run] };
    }

    private async Task<RunPageViewModel> Opened(InstanceDetail line, string? events = null)
    {
        _server.InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line)
            : string.Equals(id, "WFI-plan", StringComparison.Ordinal) ? Body(FailedPlanChild())
            : RunFixtures.Stages.Contains(id["WFI-".Length..], StringComparer.Ordinal) ? Body(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false))) : null;
        _server.InstanceEventAnswers["WFI-plan"] = new RouteAnswer(HttpStatusCode.OK, events ?? GoldenFiles.SpecShaped("workflow-instance-events", "failed-run"));
        var page = new RunPageViewModel(_server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await page.Open(_api, line.Address, line.Id, Token);
        return page;
    }

    [Fact]
    public async Task A_failed_step_shows_the_reason_from_the_events_and_a_red_edge_to_the_end()
    {
        var page = await Opened(Ended("FAILED", "failed"));

        Assert.True(page.HasFailure);
        Assert.Equal("produce", page.FailureStep.ToLowerInvariant());
        Assert.Equal("The agent stopped before it finished.", page.FailureText);
        var end = Assert.Single(page.Cards, c => c.Id == "stuck");
        Assert.Equal("Failed", end.Word);
        Assert.Contains(page.Edges, e => e.Key == "plan>stuck" && string.Equals(e.BrushKey, "Brush.Error", StringComparison.Ordinal));
        Assert.DoesNotContain(page.Edges, e => e.Key == "phase>stuck");
        Assert.Null(page.Cancel);
    }

    [Fact]
    public async Task Without_a_recorded_reason_the_block_says_so()
    {
        var page = await Opened(Ended("FAILED", "failed"), """{"events":[],"cursor":0}""");

        Assert.Equal(Labels.RunNoDetail, page.FailureText);
    }

    [Fact]
    public async Task A_run_that_did_not_work_out_reads_its_end_word_and_shows_no_red_edge()
    {
        var page = await Opened(Ended("DIDN'T WORK OUT", "unresolved"));

        var end = Assert.Single(page.Cards, c => c.Id == "stuck");
        Assert.Equal("Didn't work out", end.Word);
        Assert.Equal("Didn't work out", page.HeaderWord);
        Assert.DoesNotContain(page.Edges, e => string.Equals(e.BrushKey, "Brush.Error", StringComparison.Ordinal));
        Assert.StartsWith("Took", page.StartedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_run_has_no_failure_block_and_its_next_option_is_the_context()
    {
        var line = Ended("CANCELLED", "cancelled") with { ItemId = "ITM-01JN2" };
        var page = await Opened(line);

        Assert.False(page.HasFailure);
        Assert.Equal("Cancelled", page.HeaderWord);
        var option = Assert.Single(page.NextOptions);
        Assert.Equal(Labels.RunSeeContext, option.Text);
        Assert.Equal(new DeepLink(DeepLinkKind.ContextItem, "ITM-01JN2"), option.Link);
    }

    private async Task<RunPageViewModel> OpenedWithCatalog(InstanceDetail line, List<DeepLink>? links = null)
    {
        _server.InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line) : Body(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false)));
        var catalog = new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon));
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var page = new RunPageViewModel(_server, catalog, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        page.LinkRequested += (_, e) => links?.Add(e.Link);
        await page.Open(_api, line.Address, line.Id, Token);
        return page;
    }

    [Fact]
    public async Task Run_again_is_offered_on_an_ended_run_only_when_the_server_lists_it()
    {
        var served = await OpenedWithCatalog(Ended("FAILED", "failed") with { Actions = ["RerunInstance"] });
        var notServed = await OpenedWithCatalog(Ended("FAILED", "failed") with { Actions = [] });
        var notEnded = await OpenedWithCatalog(RunFixtures.Line("plan") with { Actions = ["RerunInstance"] });

        Assert.Equal(("RerunInstance", "Run again"), (served.Rerun!.Action, served.Rerun.Label));
        Assert.Null(notServed.Rerun);
        Assert.Null(notEnded.Rerun);
    }

    [Fact]
    public async Task Pressing_Run_again_asks_for_the_rerun_once_and_opens_the_new_run()
    {
        var links = new List<DeepLink>();
        var line = Ended("FAILED", "failed") with { Actions = ["RerunInstance"] };
        var page = await OpenedWithCatalog(line, links);

        await page.Rerun!.Press(Token);

        Assert.Equal([line.Id], _server.Reruns, StringComparer.Ordinal);
        var started = (string)GoldenFiles.Node("workflow-instances-start", "started")["instance"]!["address"]!;
        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, started), Assert.Single(links));
    }

    [Fact]
    public async Task A_run_that_is_another_attempt_links_back_to_the_old_one()
    {
        var links = new List<DeepLink>();
        var page = await OpenedWithCatalog(Ended("FAILED", "failed") with { Again = "ITM-01JKX/triage-alert/1" }, links);

        Assert.Equal(("again of ITM-01JKX/triage-alert/1", true), (page.AgainText, page.HasAgain));
        page.OpenAgain();
        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, "ITM-01JKX/triage-alert/1"), Assert.Single(links));
    }

    [Fact]
    public async Task A_first_attempt_has_no_again_link()
    {
        var page = await OpenedWithCatalog(Ended("FAILED", "failed"));

        Assert.Equal((false, string.Empty), (page.HasAgain, page.AgainText));
    }
}
