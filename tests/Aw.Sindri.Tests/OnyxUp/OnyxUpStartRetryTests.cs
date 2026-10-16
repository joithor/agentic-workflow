using Aw.Sindri.Core;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxUpStartRetryTests
{
    private const string Note = "Onyx's api_server is still starting (it restarts once while Postgres comes up); waiting";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Kernel.Error Unhealthy => SindriErrors.OnyxComposeFailed.With("`docker compose up` failed (exit 1): dependency failed to start: container sindri-onyx-api_server-1 is unhealthy");

    // Moves the fake clock in 20 s steps until the run finishes.
    private static async Task<Kernel.Result<OnyxUpResponse>> Drive(Task<Kernel.Result<OnyxUpResponse>> run, OnyxUpRig rig)
    {
        for (var i = 0; i < 2000 && !run.IsCompleted; i++)
        {
            await Task.Delay(2, Token);
            rig.Clock.Advance(OnyxStartStep.RetryInterval);
        }

        return await run;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unhealthy_dependency_is_waited_out_with_one_note()
    {
        var rig = new OnyxUpRig();
        var failed = false;
        rig.Runtime.OnCall = op =>
        {
            if (op == "Up" && !failed)
            {
                failed = true;
                rig.Runtime.FailNext("Up", Unhealthy);
            }
        };

        var result = await Drive(rig.Up(Token), rig);

        Assert.False(result.IsError, result.IsError ? result.Error.Message : "");
        Assert.Equal(2, rig.Runtime.Calls.Count(static c => c == "Up"));
        Assert.Equal(1, rig.Progress.Lines.Count(static l => l == Note));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_compose_failure_is_011_after_one_call()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.OnCall = op => { if (op == "Up") { rig.Runtime.FailNext("Up", SindriErrors.OnyxComposeFailed.With("`docker compose up` failed (exit 1): port is already allocated")); } };

        var error = (await Drive(rig.Up(Token), rig)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Equal(1, rig.Runtime.Calls.Count(static c => c == "Up"));
        Assert.DoesNotContain(Note, rig.Progress.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dependency_that_never_comes_up_is_005_at_the_deadline()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.OnCall = op => { if (op == "Up") { rig.Runtime.FailNext("Up", Unhealthy); } };

        var error = (await Drive(rig.Up(Token), rig)).Error;

        Assert.Equal("SND-ONYX-005", error.Code);
        Assert.Equal(1, rig.Progress.Lines.Count(static l => l == Note));
    }
}
