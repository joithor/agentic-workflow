using System.Text;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Desktop;

public sealed class DesktopRunTests
{
    private const string HomeShot = "/runs/desktop-1/home-40x20/app.png";
    private const string AgentsShot = "/runs/desktop-1/agents-40x20/app.png";

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_seeds_the_data_once_captures_every_screen_and_tears_down()
    {
        var harness = new DesktopRunHarness();

        var response = (await harness.Run()).Value;

        Assert.Equal(
            [
                "scripts/fx.sh up",
                "scripts/fx.sh items",
                $"bin/capture --size 40x20 --shot page:Home {HomeShot} --shot page:Agents {AgentsShot}",
                "scripts/fx.sh down",
            ],
            harness.Commands.Lines());
        var first = harness.Commands.Calls[0];
        Assert.Equal((DesktopRunHarness.Repo, "/tmp/aw-desktop-1", "bin/app"), (first.WorkingDirectory, first.Environment["APP_HOME"], first.Environment["APP_CLI"]));
        Assert.Empty(harness.Scratch.Live);
        Assert.Equal(["acquire QaStack", "acquire HeavyJob", "release HeavyJob", "release QaStack"], harness.Locks.Events);
        Assert.Equal((DesktopOutcome.Passed, 0), (response.Summary.Outcome, response.Summary.ExitCode()));
        Assert.Equal("passed: 2 screens within their limit", response.Summary.Headline);
        Assert.Equal(["display: xvfb-run (virtual X server)"], response.Summary.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_passing_run_writes_the_summary_the_report_and_four_images_per_screen()
    {
        var harness = new DesktopRunHarness();

        var response = (await harness.Run()).Value;

        var summary = response.Summary;
        Assert.Equal((DesktopSummary.SchemaName, DesktopSummary.KindName, "desktop-1", "abc123"), (summary.Schema, summary.Kind, summary.RunId.Value, summary.AppBuild));
        Assert.Equal(response.Document, Encoding.UTF8.GetString(harness.Files.Files[DesktopRunHarness.SummaryPath]));
        var home = summary.Screens[0];
        Assert.Equal(("home", "40x20", 0d, 10d, true, null), (home.Name, home.Size, home.DiffPercent, home.Threshold, home.Passed, home.Problem));
        Assert.Equal(["clock text"], home.Accepted);
        string[] images = [home.Files.App, home.Files.Design, home.Files.Diff, home.Files.SideBySide];
        Assert.All(images, image => Assert.True(harness.Files.Files.ContainsKey(image), image));
        Assert.Contains("| home | 40x20 | 0 | 10 | pass |", Encoding.UTF8.GetString(harness.Files.Files["/runs/desktop-1/report.md"]), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_mockups_are_served_from_the_design_root_and_rendered_at_the_captured_size()
    {
        var harness = new DesktopRunHarness();

        _ = await harness.Run();

        Assert.Equal(["/work/repo/docs/mockups"], harness.Site.Served);
        var session = Assert.Single(harness.Browser.Sessions);
        Assert.Equal("40x20", session.Options.Viewport.Describe());
        Assert.StartsWith("http://127.0.0.1:", session.Options.Host.Origin, StringComparison.Ordinal);
        Assert.Equal(["goto /home.html", "settle", "screenshot", "goto /agents.html", "settle", "screenshot"], session.Calls.Where(static c => c is not ("reset-scroll" or "blur")).ToList());
        Assert.Equal(0, harness.Site.Running);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_screen_over_its_own_limit_fails_the_run_and_the_headline_names_it()
    {
        var harness = new DesktopRunHarness
        {
            AppFrame = TestImages.Draw(40, 20, static (x, y) => x is >= 10 and < 15 && y is >= 5 and < 9 ? 0x000000FF : 0xFFFFFFFF),
        };

        var response = (await harness.Run()).Value;

        Assert.Equal((DesktopOutcome.Failed, 2), (response.Summary.Outcome, response.Summary.ExitCode()));
        Assert.Equal("failed: agents at 40x20 differs 2.5% (limit 2%) (1 of 2 over)", response.Summary.Headline);
        Assert.Equal([true, false], response.Summary.Screens.Select(static s => s.Passed));
        Assert.Equal([2.5, 2.5], response.Summary.Screens.Select(static s => s.DiffPercent));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_capture_that_wrote_no_frame_fails_that_screen_with_the_reason()
    {
        var harness = new DesktopRunHarness();
        _ = harness.Commands.When(static c => c.Argv[0].EndsWith("capture", StringComparison.Ordinal), _ => new CommandResult(0, string.Empty, string.Empty));

        var response = (await harness.Run()).Value;

        Assert.Equal(DesktopOutcome.Failed, response.Summary.Outcome);
        Assert.All(response.Summary.Screens, screen => Assert.StartsWith("the capture wrote no frame", screen.Problem, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_frame_of_another_size_than_the_design_fails_the_screen()
    {
        var harness = new DesktopRunHarness { AppFrame = RgbaImage.Filled(40, 24, 0xFFFFFFFF) };

        var response = (await harness.Run()).Value;

        Assert.Equal("the frames differ in size: app 40x24, design 40x20", response.Summary.Screens[0].Problem);
        Assert.False(response.Summary.Screens[0].Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fixture_command_that_fails_errors_the_run_with_the_tail_of_its_stderr_and_still_tears_down()
    {
        var harness = new DesktopRunHarness();
        _ = harness.Commands.When(static c => c.Argv is [_, "up", ..], static _ => new CommandResult(7, string.Empty, "starting\nno temporal binary\n"));

        var response = (await harness.Run()).Value;

        Assert.Equal((DesktopOutcome.Errored, 4), (response.Summary.Outcome, response.Summary.ExitCode()));
        Assert.Equal((EvidenceErrors.RunFailed.Code, "fixture up exited 7: starting | no temporal binary"), (response.Summary.Error!.Code, response.Summary.Error.Message));
        Assert.Equal(["scripts/fx.sh up", "scripts/fx.sh down"], harness.Commands.Lines());
        Assert.Empty(harness.Scratch.Live);
        Assert.Empty(harness.Browser.Sessions);
        Assert.Equal(["release HeavyJob", "release QaStack"], harness.Locks.Events.Skip(2));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_capture_that_will_not_start_errors_the_run_and_the_data_is_still_torn_down()
    {
        var harness = new DesktopRunHarness();
        _ = harness.Commands.WontStart.Add("bin/capture");

        var response = (await harness.Run()).Value;

        Assert.Equal(DesktopOutcome.Errored, response.Summary.Outcome);
        Assert.Equal("capture at 40x20: bin/capture could not start", response.Summary.Error!.Message);
        Assert.Equal("scripts/fx.sh down", harness.Commands.Lines()[^1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_design_browser_that_will_not_start_errors_the_run()
    {
        var harness = new DesktopRunHarness();
        harness.Browser.FailsToStart = true;

        var response = (await harness.Run()).Value;

        Assert.Equal((DesktopOutcome.Errored, EvidenceErrors.BrowserFailed.Code), (response.Summary.Outcome, response.Summary.Error!.Code));
        Assert.Equal(0, harness.Site.Running);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_the_screens_asked_for_are_captured_and_an_unknown_one_is_refused()
    {
        var harness = new DesktopRunHarness();

        var some = (await harness.Run(DesktopRunHarness.Request(["agents"]))).Value;
        var unknown = (await new DesktopRunHarness().Run(DesktopRunHarness.Request(["nope"]))).Value;

        Assert.Equal(["agents"], some.Summary.Screens.Select(static s => s.Name));
        Assert.Equal($"bin/capture --size 40x20 --shot page:Agents {AgentsShot}", harness.Commands.Lines()[^2]);
        Assert.Equal((DesktopOutcome.Refused, 1), (unknown.Summary.Outcome, unknown.Summary.ExitCode()));
        Assert.Contains("nope", unknown.Summary.Refusal!.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_without_a_desktop_part_is_refused_before_anything_runs()
    {
        var harness = new DesktopRunHarness().WithoutDesktopConfig();

        var response = (await harness.Run()).Value;

        Assert.Equal((DesktopOutcome.Refused, EvidenceErrors.TargetsInvalid.Code), (response.Summary.Outcome, response.Summary.Refusal!.Code));
        Assert.Empty(harness.Commands.Calls);
        Assert.Empty(harness.Scratch.Made);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_manifest_that_breaks_a_rule_is_refused_with_the_rule_named()
    {
        var harness = new DesktopRunHarness();
        _ = harness.Files.With(DesktopRunHarness.ManifestPath, "{\"schema\":\"nope\"}"u8.ToArray());

        var response = (await harness.Run()).Value;

        Assert.Equal((DesktopOutcome.Refused, EvidenceErrors.DesktopManifestInvalid.Code), (response.Summary.Outcome, response.Summary.Refusal!.Code));
        Assert.Contains("schema: must be ui-evidence.desktop-screens/1", response.Summary.Refusal.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Commands.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_manifest_that_cannot_be_read_is_an_error_and_an_earlier_summary_is_deleted()
    {
        var harness = new DesktopRunHarness();
        _ = harness.Files.With(DesktopRunHarness.SummaryPath, [1]);
        _ = harness.Files.Files.Remove(DesktopRunHarness.ManifestPath);

        var result = await harness.Run();

        Assert.Equal(EvidenceErrors.FileNotFound.Code, result.Error.Code);
        Assert.False(harness.Files.Files.ContainsKey(DesktopRunHarness.SummaryPath));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lock_that_stays_busy_runs_nothing_and_records_nothing()
    {
        var harness = new DesktopRunHarness();
        _ = harness.Locks.AlwaysBusy.Add(LockKind.QaStack);

        var result = await harness.Run();

        Assert.Equal(EvidenceErrors.LockBusy.Code, result.Error.Code);
        Assert.Empty(harness.Commands.Calls);
        Assert.False(harness.Files.Files.ContainsKey(DesktopRunHarness.SummaryPath));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_run_still_tears_the_data_down()
    {
        var harness = new DesktopRunHarness();
        using var cancel = new CancellationTokenSource();
        _ = harness.Commands.When(static c => c.Argv[0].EndsWith("capture", StringComparison.Ordinal), _ =>
        {
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        });

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await harness.Run(cancellationToken: cancel.Token));

        Assert.Equal("scripts/fx.sh down", harness.Commands.Lines()[^1]);
        Assert.Empty(harness.Scratch.Live);
    }
}
