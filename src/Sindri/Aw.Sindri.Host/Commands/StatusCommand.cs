using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetInstance;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow status &lt;address&gt; [--json]' (engine spec §6.3, §6.4; engine P4 unit (e)): the §6.4 layout, or with
/// '--json' the instance as 'GetInstance' returns it. Exit 0; 1 when Sindri isn't running; 2 on any other error
/// (SND-WF-001 no such instance, SND-API-002 no worker answered, SND-CLI-003 a malformed address).
/// </summary>
internal static class StatusCommand
{
    private const string Usage = "usage: sindri workflow status <address> [--json]";

    internal static async Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        if (args.Where(static a => a != "--json").ToArray() is not [var text] || !InstanceAddress.TryFrom(text, out var address))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"the one argument is the instance address ('{{item}}/{{recipe}}/{{attempt}}', e.g. T-1/step/1) ({Usage})")).ConfigureAwait(false);
        }

        var got = await services.GetRequiredService<IService<GetInstanceRequest, GetInstanceResponse>>().Handle(new GetInstanceRequest(address), ct).ConfigureAwait(false);
        if (got.IsError)
        {
            return await output.Error(got.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Json(got.Value.Instance, 0).ConfigureAwait(false)
            : await output.Text(StatusLayout.Render(got.Value.Instance, services.GetRequiredService<TimeProvider>().GetUtcNow()), 0).ConfigureAwait(false);
    }
}
