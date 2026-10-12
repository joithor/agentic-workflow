using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'workflow' verb group (engine spec rev 8 §6.3): every verb of §6.3 has its row, each a client that calls one
/// slice. Unit (b) builds 'start' (<see cref="WorkflowStartCommand"/>) and 'answer', 'command' and 'cancel'
/// (<see cref="ApplyCommandVerbs"/>); unit (c) 'complete' (<see cref="CompleteCommand"/>); unit (e) 'status'
/// (<see cref="StatusCommand"/>), 'list' (<see cref="ListCommand"/>) and 'events' (<see cref="EventsCommand"/>).
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
        ["start", .. var rest] => WorkflowStartCommand.Run(rest, services, output, ct),
        ["status", .. var rest] => StatusCommand.Run(rest, services, output, ct),
        ["list", .. var rest] => ListCommand.Run(rest, services, output, ct),
        ["events", .. var rest] => EventsCommand.Run(rest, services, output, ct),
        ["answer", .. var rest] => ApplyCommandVerbs.Answer(rest, services, output, ct),
        ["command", .. var rest] => ApplyCommandVerbs.Command(rest, services, output, ct),
        ["cancel", .. var rest] => ApplyCommandVerbs.Cancel(rest, services, output, ct),
        ["complete", .. var rest] => CompleteCommand.Run(rest, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["workflow", .. args])}' is not a sindri command ({Usage})")),
    };
}
