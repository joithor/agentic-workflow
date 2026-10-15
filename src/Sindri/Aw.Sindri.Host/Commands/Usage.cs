namespace Aw.Sindri.Host.Commands;

/// <summary>The usage text: one line per verb, then the exit codes.</summary>
internal static class Usage
{
    private static readonly string[] _verbs =
    [
        // ---- one line per verb ----
        "usage: sindri [--headless]           start the control plane if it isn't running, then print its summary (never from an agent session)",
        "       sindri serve                  run the control plane in the foreground (what 'sindri' starts; launchd target; refused in an agent session)",
        "       sindri status [--json]        the running control plane's status (exit 1 when it isn't running)",
        "       sindri stop                   stop it and release the host lease",
        "       sindri profile validate [--json]",
        "       sindri profile approve [--no-start] [<hash>]   without a hash: show the hash and what changed; with it: approve (needs a terminal); starts the control plane unless --no-start",
        "       sindri doctor [--json]        lease, ledger, profile, api, temporal and context checks, and the pinned harness probe; runs without the control plane",
        "       sindri doctor --probe [--harness <name>]  this process's agent variables (names only), ancestry and harness version, as JSON",
        "       sindri mcp register [--repo NAME] [--apply] [--json]   the per-repo Claude Code registration of /sindri/mcp (--apply: a human at a terminal)",
        "       sindri context query <text> [--node K | --item I | --symbol S] [--depth N] [--budget N] [--draft] [--json]   cited context from the running control plane (exit 1: not running, unknown anchor, refused)",
        "       sindri context show <key> [--json]   one map node (a key, alias or name), its aliases and edges (exit 1: unknown or ambiguous, no map)",
        "       sindri context neighbors <key> [--depth N] [--json]   the nodes within N edges (0-4, default 1) either way",
        "       sindri context path <a> <b> [--json]   the shortest path of at most 8 edges between two nodes",
        "       sindri context eval probes [--file F] [--json]   ask each probe, print precision@5 and citation correctness beside the baseline run (exit 1: regressed, SND-CONTEXT-038; exit 2: a probe's query failed, nothing saved)",
        "       sindri context sources status [--json]   each context source's state and whether sessions may read context",
        "       sindri context map import <file>   check a map file, show its hash, type the challenge it shows ('import map <hash6>') to make it the current map (a terminal only)",
        "       sindri context map export [<path>] [--json]   write the current map under $AW_SINDRI_HOME/context/exports/",
        "       sindri context map status [--json]   the current map version and its counts (exit 1: no map yet, SND-CONTEXT-030)",
        "       sindri context sources sync      pending:S-5B (repo docs refresh through the context-docs-refresh service)",
        "       sindri recipe validate <file> [--json]   check a recipe in-process: rule lines, then its hash (exit 0 pass, 1 fail, 2 error)",
        "       sindri recipe eval <file> [--json]       run a recipe's eval cases in-process (exit 0 pass, 1 fail, 2 error)",
        "       sindri recipe eval --scaffold <file>     print a happy-path eval case for the recipe",
        "       sindri recipe eval --record <address> --out <file>   an instance's history as an eval case (needs the server, P4)",
        "       sindri recipe register <file>            register a recipe by hash: 'registered <id>@<hash> (unapproved)'",
        "       sindri recipe show <id@hash>             the recipe's line and its approval preview",
        "       sindri recipe list [--stale] [--json]    one line per registered recipe; --stale: approved ones that drifted or pin a stale child",
        "       sindri recipe approve <file|id@hash>     preview, then type 'approve <id> <version>' at a terminal: 'approved <id>@<hash12>'",
        "       sindri recipe revoke <id@hash>           preview (parks, cascade, parents), then type 'revoke <id>@<hash12>' at a terminal",
        "       sindri workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--temporal <host:port>] [--no-wait]   report an open desk run (exit 0 ok, 1 Temporal not running, 2 error)",
        "       sindri workflow list [--query <q>] [--temporal <host:port>]   the open instances, one a line",
    ];

    internal static readonly string Text = string.Join('\n', [.. _verbs, "exit codes: 0 ok, 1 attention needed, 2 error"]);
}
