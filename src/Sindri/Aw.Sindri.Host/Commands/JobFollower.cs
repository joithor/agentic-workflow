using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Commands;

/// <summary>Follows one started job to its end (API P6 unit H); stub, red.</summary>
internal sealed class JobFollower
{
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    internal static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(5);

    internal JobFollower(ControlPlaneClient client, Output output, TimeProvider clock)
    {
        Client = client;
        Out = output;
        Clock = clock;
    }

    private ControlPlaneClient Client { get; }

    private Output Out { get; }

    private TimeProvider Clock { get; }

    internal Task<int> Follow(JobStartedResponse started, bool json, CancellationToken interrupt, CancellationToken again)
    {
        _ = (Client, Out, Clock, started, json, interrupt, again);
        throw new NotSupportedException();
    }
}
