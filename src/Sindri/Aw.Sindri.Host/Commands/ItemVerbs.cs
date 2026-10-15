namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri queue', 'items', 'item add' and 'item close' (API P3 unit H, plan G17): clients of the item routes. Human verbs
/// start the control plane unless --no-start; inside an agent session they never do (API spec §7).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane before a command, or names why not; null: never start it.</param>
internal sealed class ItemVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Kernel.Error?>>? ensureRunning = null)
{
    internal Task<int> Run(string[] args, CancellationToken ct)
    {
        _ = (client, output, ensureRunning, args, ct);
        throw new NotSupportedException("item verbs are not written yet");
    }
}
