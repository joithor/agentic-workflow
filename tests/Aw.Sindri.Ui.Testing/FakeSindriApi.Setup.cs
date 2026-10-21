using Aw.Kernel;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The setup read of the fake (setup-once spec §7): it answers a golden, the same text the handler serves (Golden/Api/setup, api #458's own).
/// </summary>
internal sealed partial class FakeSindriApi
{
    private int _setupCalls;

    /// <summary>GET /sindri/setup's answer: every step done, so a test that is not about setup sees no Setup card on Home.</summary>
    public RouteAnswer SetupAnswer { get; set; } = RouteAnswer.Api("setup", "all-done");

    /// <summary>How many times the setup list was read.</summary>
    public int SetupCalls => Volatile.Read(ref _setupCalls);

    /// <summary>When set, a setup read waits here (after it was counted) until the gate is completed, then answers what <see cref="SetupAnswer"/> is by then: a read in flight while something else happens.</summary>
    public TaskCompletionSource? SetupGate { get; set; }

    public async Task<Result<SetupView>> Setup(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _setupCalls);
        if (SetupGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<SetupView>(api, SetupRoutes.Read, SetupAnswer);
    }
}
