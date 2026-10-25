using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.GetSetup;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri setup [--json]': where "set up Sindri once" stands, a client of GET /sindri/setup. It reads the route and never starts the control plane (API spec §7), and it only reports:
/// every control that moves a step forward is the app's. Exit 0 when every step is done or skipped; 1 when a step is still to do, waits for you or failed, or when Sindri isn't running (SND-API-001);
/// 2 on usage or any other error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class SetupVerbs(ControlPlaneClient client, Output output)
{
    internal const string Usage = "setup [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Any(static a => a != "--json"))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'setup' takes no argument but --json ({Usage})")).ConfigureAwait(false);
        }

        var answer = await client.Get<GetSetupResponse>("/sindri/setup", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Value.Steps.All(static s => s.State is SetupState.Done or SetupState.Skipped) ? 0 : 1;
        return args.Length > 0
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(SetupText.Render(answer.Value.Value), code).ConfigureAwait(false);
    }
}
