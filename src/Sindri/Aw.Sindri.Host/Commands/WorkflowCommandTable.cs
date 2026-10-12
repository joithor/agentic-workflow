using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'workflow' verb group (engine spec rev 8 §6.3). Engine P4 contract: every verb of §6.3 has its row. Unit (e)
/// builds 'status' (<see cref="StatusCommand"/>), 'list' (<see cref="ListCommand"/>) and 'events'
/// (<see cref="EventsCommand"/>), unit (c) 'complete' (<see cref="CompleteCommand"/>); the others are stubs, each
/// naming the unit that builds it: 'start' (b), 'answer' (b), 'command' (b) and 'cancel' (b). Each calls one slice.
/// </summary>
internal static class WorkflowCommandTable
{
    private const string Usage =
        "usage: sindri workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]... | status <address> [--json] | "
        + "list [--needs-me | --item <id> | --state <s>] [--query <q>] [--json] | events <address> [--follow] [--json] | "
        + "answer <address> [--text …|--file …] [--grant n | --grant-tokens n | --accept | --reject | --resume] | "
        + "command <address> <verb> | cancel <address> | complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--nonce <n>] [--json]";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["start", ..] => Unbuilt("start", "unit (b)"),
        ["status", .. var rest] => StatusCommand.Run(rest, services, output, ct),
        ["list", .. var rest] => ListCommand.Run(rest, services, output, ct),
        ["events", .. var rest] => EventsCommand.Run(rest, services, output, ct),
        ["answer", ..] => Unbuilt("answer", "unit (b)"),
        ["command", ..] => Unbuilt("command", "unit (b)"),
        ["cancel", ..] => Unbuilt("cancel", "unit (b)"),
        ["complete", .. var rest] => CompleteCommand.Run(rest, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["workflow", .. args])}' is not a sindri command ({Usage})")),
    };

    private static Task<int> Unbuilt(string verb, string unit) =>
        throw new NotSupportedException($"'sindri workflow {verb}' is engine P4 {unit}'s and is not built yet");
}
