using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri request', 'requests', 'decide' and 'needs-you' (API P4 unit H): clients of the request, decision and needs-you routes.</summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class RequestVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal Task<int> Run(string[] args, CancellationToken ct)
    {
        _ = (client, output, ensureRunning, args, ct);
        throw new NotSupportedException("red");
    }
}
