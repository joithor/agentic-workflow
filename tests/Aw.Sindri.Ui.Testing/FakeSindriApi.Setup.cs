using Aw.Kernel;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The setup read of the fake (setup-once spec §7): it answers a golden, the same text the handler serves. The goldens are spec-shaped (Golden/SpecShaped/setup, copied
/// from api #458's own; PROVISIONAL until that PR merges and the files move to Golden/Api).
/// </summary>
internal sealed partial class FakeSindriApi
{
    private int _setupCalls;

    /// <summary>GET /sindri/setup's answer: every step done, so a test that is not about setup sees no Setup card on Home.</summary>
    public RouteAnswer SetupAnswer { get; set; } = RouteAnswer.SpecShaped("setup", "all-done");

    /// <summary>How many times the setup list was read.</summary>
    public int SetupCalls => Volatile.Read(ref _setupCalls);

    public Task<Result<SetupView>> Setup(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _setupCalls);
        return Task.FromResult(Answer<SetupView>(api, SetupRoutes.Read, SetupAnswer));
    }
}
