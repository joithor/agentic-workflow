namespace Aw.Sindri.Host.Commands;

/// <summary>The usage text.</summary>
internal static class Usage
{
    internal static readonly string Text = string.Join('\n',
        "usage: sindri [--headless]           start the control plane if it isn't running, then print its summary",
        "       sindri serve                  run the control plane in the foreground (what 'sindri' starts; launchd target)",
        "       sindri status [--json]        the running control plane's status (exit 1 when it isn't running)",
        "       sindri stop                   stop it and release the host lease",
        "       sindri profile validate [--json]",
        "       sindri profile approve [<hash>]   without a hash: show the hash and what changed; with it: approve (needs a terminal)",
        "       sindri doctor [--json]        lease, ledger, profile, api and temporal checks; runs without the control plane",
        "       sindri recipe validate <file> [--json]   check a recipe in-process: rule lines, then its hash (exit 0 pass, 1 fail, 2 error)",
        "       sindri recipe eval <file> [--json]       run a recipe's eval cases in-process (exit 0 pass, 1 fail, 2 error)",
        "       sindri recipe eval --scaffold <file>     print a happy-path eval case for the recipe",
        "       sindri recipe eval --record <address> --out <file>   an instance's history as an eval case (needs the server, P4)",
        "       sindri workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--temporal <host:port>] [--no-wait]",
        "                                     report an open desk run (exit 0 ok, 1 Temporal not running, 2 error)",
        "       sindri workflow list [--query <q>] [--temporal <host:port>]   the open instances, one a line",
        "exit codes: 0 ok, 1 attention needed, 2 error");
}
