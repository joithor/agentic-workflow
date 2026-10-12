using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Desk;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'workflow' verb group (engine spec rev 8 §6.3). Engine P4 contract: every verb of §6.3 has its row. 'list' is
/// P4a's visibility query (unit (e) moves it to the §6.3 filters and '--needs-me'); the others are stubs, each naming
/// the unit that builds it: 'start' (b), 'status' (e), 'events' (e), 'answer' (b), 'command' (b) and 'cancel' (b).
/// 'complete' is unit (c)'s <see cref="CompleteCommand"/>. Each becomes a client of 'sindri serve' that calls one slice through the API chain.
/// </summary>
internal static class WorkflowCommandTable
{
    private const string ListUsage = "usage: sindri workflow list [--query <visibility query>] [--temporal <host:port>]";

    private const string Usage =
        "usage: sindri workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]... | status <address> [--json] | "
        + "list [--needs-me | --item <id> | --state <s>] [--json] | events <address> [--follow] [--json] | "
        + "answer <address> [--text …|--file …] [--grant n | --grant-tokens n | --accept | --reject | --resume] | "
        + "command <address> <verb> | cancel <address> | complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--nonce <n>] [--json]";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["start", ..] => Unbuilt("start", "unit (b)"),
        ["status", ..] => Unbuilt("status", "unit (e)"),
        ["list", .. var rest] => List(rest, services, output, ct),
        ["events", ..] => Unbuilt("events", "unit (e)"),
        ["answer", ..] => Unbuilt("answer", "unit (b)"),
        ["command", ..] => Unbuilt("command", "unit (b)"),
        ["cancel", ..] => Unbuilt("cancel", "unit (b)"),
        ["complete", .. var rest] => CompleteCommand.Run(rest, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["workflow", .. args])}' is not a sindri command ({Usage})")),
    };

    private static Task<int> Unbuilt(string verb, string unit) =>
        throw new NotSupportedException($"'sindri workflow {verb}' is engine P4 {unit}'s and is not built yet");

    private static async Task<int> List(string[] rest, IServiceProvider services, Output output, CancellationToken ct)
    {
        var options = rest.Chunk(2).ToList();
        if (options.FirstOrDefault(static o => o is not [("--query" or "--temporal"), _]) is { } bad)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'{string.Join(' ', bad)}' isn't an option ({ListUsage})")).ConfigureAwait(false);
        }

        string? Value(string flag) => options.LastOrDefault(o => o[0] == flag)?[1];
        var listed = await services.GetRequiredService<IWorkflowInstances>().List(Value("--query"), Value("--temporal"), ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return await output.Error(listed.Error).ConfigureAwait(false);
        }

        var lines = listed.Value.Select(static r => $"{r.Address}  {r.State ?? "?"}{(r.NeedsYou ? "  needs-you" : string.Empty)}\n");
        return await output.Text(string.Concat(lines), 0).ConfigureAwait(false);
    }
}
