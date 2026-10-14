using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'workflow' verb group (engine spec rev 8 §6.3): every verb of §6.3 has its row, each a client that calls one
/// slice. The writes (start, answer, command, cancel, complete) call the control plane's API, so the command chain
/// classes the caller (API P1); the reads run their queries in process. Unit (b) builds 'start' (<see cref="WorkflowStartCommand"/>) and 'answer', 'command' and 'cancel'
/// (<see cref="ApplyCommandVerbs"/>); unit (c) 'complete' (<see cref="CompleteCommand"/>); unit (e) 'status'
/// (<see cref="StatusCommand"/>), 'list' (<see cref="ListCommand"/>) and 'events' (<see cref="EventsCommand"/>).
/// </summary>
internal static class WorkflowCommandTable
{
    internal const string UsageText =
        "usage: sindri workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]... [--json] | status <address> [--json] | "
        + "list [--needs-me | --item <id> | --state <s>] [--query <q>] [--json] | events <address> [--follow] [--json] | "
        + "answer <address> [--text …|--file …] [--grant n | --grant-tokens n | --accept | --reject | --resume] [--nonce <n>] [--json] | "
        + "command <address> <verb> [--nonce <n>] [--json] | cancel <address> [--nonce <n>] [--json] | complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--nonce <n>] [--json]";

    /// <summary>Runs one 'workflow' verb: the writes as clients of the API through <paramref name="desk"/>, the reads in process.</summary>
    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, RecipeDesk desk, CancellationToken ct) => args switch
    {
        ["start", .. var rest] => WorkflowStartCommand.Run(rest, desk, services, output, ct),
        ["status", .. var rest] => StatusCommand.Run(rest, services, output, ct),
        ["list", .. var rest] => ListCommand.Run(rest, services, output, ct),
        ["events", .. var rest] => EventsCommand.Run(rest, services, output, ct),
        ["answer", .. var rest] => new ApplyCommandVerbs(desk, services, output).Answer(rest, ct),
        ["command", .. var rest] => new ApplyCommandVerbs(desk, services, output).Command(rest, ct),
        ["cancel", .. var rest] => new ApplyCommandVerbs(desk, services, output).Cancel(rest, ct),
        ["complete", .. var rest] => CompleteCommand.Run(rest, desk, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["workflow", .. args])}' is not a sindri command ({UsageText})")),
    };
}
