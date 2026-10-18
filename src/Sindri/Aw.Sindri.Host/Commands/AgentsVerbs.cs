namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri agents' and 'sindri agents show' (collector spec CI-8): clients of the session routes; they never start the control plane.</summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class AgentsVerbs(ControlPlaneClient client, Output output)
{
    internal Task<int> Run(string[] args, CancellationToken ct)
    {
        _ = (client, output, args, ct);
        throw new NotSupportedException();
    }
}
