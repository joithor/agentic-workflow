using Aw.Sindri.Features.GetLaunchPicks;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri launch picks [--item X] [--json]' (model-pick spec §6.1): a client of GET /sindri/launch/picks. Read-only, so it never
/// starts the control plane: when serve is down it exits 1 with SND-API-001, like 'sindri status'. One line per pick, oldest first;
/// '--json' prints the route's answer.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class LaunchPicksVerb(ControlPlaneClient client, Output output)
{
    internal const string Usage = "launch picks [--item X] [--json]";

    internal Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(output);
        ct.ThrowIfCancellationRequested();
        throw new NotSupportedException("launch picks is not built yet");
    }

    /// <summary>The text form: one line per pick.</summary>
    /// <param name="picks">The route's answer.</param>
    /// <returns>The lines, or 'no picks'.</returns>
    internal static string Render(LaunchPicks picks)
    {
        ArgumentNullException.ThrowIfNull(picks);
        throw new NotSupportedException("launch picks is not built yet");
    }
}
