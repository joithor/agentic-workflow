using Aw.Evidence.Core;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Testing;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Tests.Parity;

public sealed class ParityTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Parity_writes_parity_json_with_every_frame_and_its_images()
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest());

        var result = await harness.Run(ParityHarness.Request() with { DbProvenance = DbProvenance.Seeded, AppBuild = "abc123" });

        Assert.True(result.Value.Passed);
        var summary = result.Value.Summary;
        Assert.Equal((ParitySummary.SchemaName, "parity-1", Provenance.Seeded, "abc123"), (summary.Schema, summary.RunId.Value, summary.Provenance, summary.AppBuild));
        var frame = Assert.Single(summary.Frames);
        Assert.Equal(("default", 0L, 100L), (frame.Name, frame.DiffPixels, frame.Total));
        Assert.Equal(new Core.Geometry.Offset(5, 5), frame.ImplementationOrigin);
        string[] images = [frame.Files.Design, frame.Files.Implementation, frame.Files.Diff, frame.Files.SideBySide];
        Assert.All(images, i => Assert.True(harness.Files.Files.ContainsKey(i), i));
        Assert.Equal(AwJson.Serialize(summary, TestJson.Options), harness.Files.Files["/runs/parity-1/parity.json"]);
        Assert.Equal(["acquire QaStack", "acquire HeavyJob", "release HeavyJob", "release QaStack"], harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_lists_each_failed_geometry_check()
    {
        var frame = ManifestBuilder.Frame() with
        {
            ExpectBoxes = [new BoxExpectation(ElementSelector.ByTestId(TestId.From("card")), new ExpectedGeometry(W: 99))],
        };
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest(frame));

        var result = await harness.Run(ParityHarness.Request());

        Assert.False(result.Value.Passed);
        Assert.Equal(["default testId:card: w 10 != 99"], result.Value.GeometryFailures);
        Assert.Equal(Provenance.Unknown, result.Value.Summary.Provenance);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_deletes_an_earlier_parity_json_and_fails_the_run_when_the_anchor_is_missing()
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest());
        _ = harness.Files.With("/runs/parity-1/parity.json", "{}"u8.ToArray());
        harness.Page.Boxes.Clear();

        var result = await harness.Run(ParityHarness.Request());

        Assert.Equal((EvidenceErrors.RunFailed.Code, "frame default: anchor testId:card not found"), (result.Error.Code, result.Error.Message));
        Assert.False(harness.Files.Files.ContainsKey("/runs/parity-1/parity.json"));
        Assert.False(harness.Locks.IsHeld(LockKind.QaStack));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_fails_the_run_when_the_region_runs_past_the_viewport()
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest());
        harness.Page.Boxes["testId:card"] = new Core.Geometry.Box(35, 35, 10, 10);

        var result = await harness.Run(ParityHarness.Request());

        Assert.Equal(ErrorKind.Unavailable, result.Error.Kind);
        Assert.Contains("viewport of at least 45x45, manifest viewport is 40x40", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_resolves_a_relative_design_export_against_the_manifest_directory()
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest(ManifestBuilder.Frame() with { DesignPng = "designs/local.png" }));
        _ = harness.Files.With("/work/designs/local.png", harness.Files.Files["/designs/default.png"]);

        var result = await harness.Run(ParityHarness.Request());

        Assert.True(result.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_names_the_manifest_file_and_the_json_location_of_a_bad_manifest()
    {
        var harness = new ParityHarness();
        _ = harness.Files.With(ParityHarness.ManifestPath, """{"route":"/","frames":[],"zoom":2}"""u8.ToArray());

        var result = await harness.Run(ParityHarness.Request());

        Assert.Equal((EvidenceErrors.ManifestInvalid.Code, "manifest /work/manifest.json: $.zoom: unknown property"), (result.Error.Code, result.Error.Message));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_refuses_a_write_looking_frame_step_before_taking_a_lock()
    {
        var frame = ManifestBuilder.Frame() with { Steps = [ScriptBuilder.Click("submit-form")] };
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest(frame));

        var result = await harness.Run(ParityHarness.Request());

        Assert.Equal(EvidenceErrors.StepsRefused.Code, result.Error.Code);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_reads_sign_in_credentials_only_from_the_environment()
    {
        var harness = new ParityHarness().WithManifest(A.Manifest().WithLogin(ManifestBuilder.Login()).Build() with { Viewport = new(40, 40) });

        var result = await harness.Run(ParityHarness.Request());

        Assert.Equal((EvidenceErrors.CredentialsMissing.Code, "missing credentials in environment: APP_EMAIL, APP_PASSWORD"), (result.Error.Code, result.Error.Message));
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_signs_in_and_never_echoes_a_credential_in_a_run_failure()
    {
        var frame = ManifestBuilder.Frame() with { WaitFor = ElementSelector.ByText("qa@example.com") };
        var harness = new ParityHarness().WithManifest(A.Manifest().WithFrame(frame).WithLogin(ManifestBuilder.Login()).Build() with { Viewport = new(40, 40) });
        harness.Secrets["APP_EMAIL"] = "qa@example.com";
        harness.Secrets["APP_PASSWORD"] = "hunter2";

        var result = await harness.Run(ParityHarness.Request());

        Assert.Equal(EvidenceErrors.RunFailed.Code, result.Error.Code);
        Assert.DoesNotContain("qa@example.com", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("[redacted]", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal("sign-in", harness.Browser.Sessions.Single().Calls[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_is_lock_busy_without_opening_a_browser()
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest());
        _ = harness.Locks.AlwaysBusy.Add(LockKind.QaStack);

        var result = await harness.Run(ParityHarness.Request());

        Assert.Equal(EvidenceErrors.LockBusy.Code, result.Error.Code);
        Assert.Empty(harness.Browser.Sessions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_releases_both_locks_in_reverse_order_when_the_run_throws()
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest());
        harness.Page.ScreenshotThrows = new InvalidOperationException("the browser crashed");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await harness.Run(ParityHarness.Request()));

        Assert.Equal(["acquire QaStack", "acquire HeavyJob", "release HeavyJob", "release QaStack"], harness.Locks.Events);
        Assert.False(harness.Locks.IsHeld(LockKind.HeavyJob));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_releases_both_locks_in_reverse_order_when_cancelled_mid_run()
    {
        using var cancel = new CancellationTokenSource();
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest(ManifestBuilder.Frame() with { Steps = [ScriptBuilder.Click("open")] }));
        _ = harness.Page.TestIds.Add("open");
        harness.Page.OnClick["testId:open"] = _ => cancel.Cancel();   // Ctrl-C arrives while a frame step runs

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await harness.Interruptible(ParityHarness.Request(), cancel.Token));

        Assert.Equal(["acquire QaStack", "acquire HeavyJob", "release HeavyJob", "release QaStack"], harness.Locks.Events);
        Assert.False(harness.Locks.IsHeld(LockKind.HeavyJob));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_refuses_a_write_looking_route_before_taking_a_lock()
    {
        var harness = new ParityHarness().WithManifest(ParityHarness.Manifest() with { Route = "/invites/accept?token=abc" });

        var result = await harness.Run(ParityHarness.Request());

        Assert.Equal(EvidenceErrors.StepsRefused.Code, result.Error.Code);
        Assert.Empty(harness.Locks.Events);
    }
}
