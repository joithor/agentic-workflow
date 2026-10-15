using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow status &lt;instance&gt; [--json]' (engine spec §6.3, §6.4; engine P4 unit (e)): the §6.4 layout, or with
/// '--json' the instance as 'GetInstance' returns it. A client of serve's routes (review run 2): the reference resolves to
/// the instance's id through 'GET /workflow/instances?address=', then 'GET /workflow/instances/{id}' answers; it never
/// starts serve. Exit 0; 1 when Sindri isn't running (SND-API-001); 2 on any other error
/// (SND-WF-001 no such instance or a reference that names none, SND-API-002 no worker answered, SND-CLI-003 a missing argument). A bare item
/// names the root of its latest attempt, as in the write verbs (spec §6.1).
/// </summary>
internal static class StatusCommand
{
    private const string Usage = "usage: sindri workflow status <instance> [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        if (args.Where(static a => a != "--json").ToArray() is not [{ Length: > 0 } text] || text.StartsWith("--", StringComparison.Ordinal))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"the one argument is the instance: its address ('{{item}}/{{recipe}}/{{attempt}}', e.g. T-1/step/1) or a bare item ({Usage})")).ConfigureAwait(false);
        }

        var id = await InstanceReads.IdOf(desk, text, ct).ConfigureAwait(false);
        var got = id.IsError ? id.Error : await desk.Client.Get<GetInstanceResponse>($"{InstanceReads.Instances}/{InstanceRoute.Segment(id.Value.Value)}", ct).ConfigureAwait(false);
        if (got.IsError)
        {
            return await output.Error(InstanceReads.NotRunning(got.Error)).ConfigureAwait(false);
        }

        return json
            ? await output.Json(got.Value.Value.Instance, 0).ConfigureAwait(false)
            : await output.Text(StatusLayout.Render(got.Value.Value.Instance, services.GetRequiredService<TimeProvider>().GetUtcNow()), 0).ConfigureAwait(false);
    }
}
