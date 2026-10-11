using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Testing;
using Aw.Kernel;

namespace Aw.Evidence.Tests.RunScript;

public sealed class RunScriptRefusalTests
{
    private static RunScriptHarness WithSave()
    {
        var harness = new RunScriptHarness();
        _ = harness.WithScript(A.Script().WithStep(ScriptBuilder.Click("save-button")).Build());
        return harness;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_refuses_a_write_looking_step_on_a_preview_host_before_taking_a_lock()
    {
        var harness = WithSave();

        var result = await harness.Run(A.RunScriptRequest().OnHost("https://pr-12.vitalize.build").Build());

        Assert.Equal(EvidenceErrors.StepsRefused.Code, result.Error.Code);
        Assert.Empty(harness.Locks.Events);
        Assert.Empty(harness.Browser.Sessions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_runs_write_looking_steps_on_a_preview_host_when_writes_are_allowed()
    {
        var harness = WithSave();
        _ = harness.Page.TestIds.Add("save-button");

        var result = await harness.Run(A.RunScriptRequest().OnHost("https://pr-12.vitalize.build", allowWrites: true).Build());

        Assert.Equal("https://pr-12.vitalize.build", result.Value.Summary.Host);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_refuses_a_host_that_is_not_local_or_a_preview()
    {
        var harness = WithSave();

        var result = await harness.Run(A.RunScriptRequest().OnHost("https://app.vitalize.build").Build());

        Assert.Equal(EvidenceErrors.HostRefused.Code, result.Error.Code);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_refuses_a_malformed_script_before_taking_a_lock()
    {
        var harness = new RunScriptHarness();
        _ = harness.Files.With(RunScriptRequestBuilder.ScriptPath, "{\"route\":"u8.ToArray());

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.Equal(EvidenceErrors.ScriptInvalid.Code, result.Error.Code);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_is_lock_busy_without_opening_a_browser_and_leaves_nothing_held()
    {
        var harness = WithSave();
        _ = harness.Locks.AlwaysBusy.Add(LockKind.HeavyJob);

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.Equal((EvidenceErrors.LockBusy.Code, ErrorKind.Timeout), (result.Error.Code, result.Error.Kind));
        Assert.Empty(harness.Browser.Sessions);
        Assert.False(harness.Locks.IsHeld(LockKind.QaStack));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_releases_the_locks_when_the_browser_fails_to_start()
    {
        var harness = WithSave();
        harness.Browser.FailsToStart = true;

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.Equal(ErrorKind.Unavailable, result.Error.Kind);
        Assert.False(harness.Locks.IsHeld(LockKind.QaStack));
        Assert.False(harness.Locks.IsHeld(LockKind.HeavyJob));
        Assert.False(harness.Files.Files.ContainsKey("/runs/run-1/summary.json"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("work/script.json", "/runs/run-1")]
    [InlineData("/work/script.json", "runs/run-1")]
    [InlineData("/work/script.json", "/")]
    public async Task RunScript_needs_absolute_paths_and_a_named_run_directory(string script, string runDirectory)
    {
        var harness = WithSave();

        var result = await harness.Run(A.RunScriptRequest().Build() with { ScriptPath = script, RunDirectory = runDirectory });

        Assert.Equal(EvidenceErrors.Invalid.Code, result.Error.Code);
    }
}
