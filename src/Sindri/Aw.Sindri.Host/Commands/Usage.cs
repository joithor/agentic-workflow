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
        "       sindri views rebuild [--no-start]   replay the ledger into the views (the fix when they stall); starts the control plane unless --no-start",
        "       sindri queue [--json] [--no-start]   the queue's groups, one line per item; exit 1 while the views rebuild or stall; starts the control plane unless --no-start",
        "       sindri items [--state <s>] [--repo <r>] [--json] [--no-start]   the item list, the oldest first",
        "       sindri item add <title> [--repo <r>] [--no-start]   create an item: 'ITM-… created (sequence N)'",
        "       sindri item close <item> [--cancelled] [--version <n>] [--no-start]   close an item (id or tracker key) as done, or cancelled; without --version it closes the version it reads first",
        "       sindri doctor [--json]        lease, ledger, views, profile, api, temporal and context checks, and the pinned harness probe; runs without the control plane",
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
        "       sindri recipe show <id@hash> [--json]    the recipe's line, its urgency, inputs, outputs and path order, and its approval preview; --json: the same as fields",
        "       sindri recipe list [--stale] [--json]    one line per registered recipe; --stale: approved ones that drifted or pin a stale child",
        "       sindri recipe approve <file|id@hash>     preview, then type 'approve <id> <version>' at a terminal: 'approved <id>@<hash12>'",
        "       sindri recipe revoke <id@hash>           preview (parks, cascade, parents), then type 'revoke <id>@<hash12>' at a terminal",
        "       sindri workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]... [--json]   start an approved recipe on an item (exit 0 started or found, 1 not running, 2 invalid, refused or error)",
        "       sindri workflow status <address> [--json]   one instance's state, open runs and waits (exit 0 ok, 1 not running, 2 invalid, refused or error: no such instance, malformed address)",
        "       sindri workflow list [--needs-me | --item <id> | --state <s>] [--query <q>] [--json]   one status header per instance; --needs-me: decisions, then tasks (exit 0 ok, 1 not running, 2 invalid, refused or error)",
        "       sindri workflow events <address> [--follow] [--json]   the instance's events from the ledger, oldest first; --follow keeps reading until interrupted (exit 0 ok, 1 not running, 2 invalid, refused or error)",
        "       sindri workflow answer <address> [--text <t> | --file <f>] [--grant <n> | --grant-tokens <n> | --accept | --reject | --resume] [--nonce <n>] [--json]   answer a parked instance (grant, grant-tokens, accept: type the challenge at a terminal; exit 0 applied, 1 not running, 2 invalid, refused or error)",
        "       sindri workflow command <address> <verb> [--nonce <n>] [--json]   send a recipe command to the instance (exit 0 applied, 1 not running, 2 invalid, refused or error)",
        "       sindri workflow cancel <address> [--nonce <n>] [--json]   cancel the instance (exit 0 applied, 1 not running, 2 invalid, refused or error)",
        "       sindri workflow takeover <address> [--nonce <n>] [--json]   take the instance over: its timer pauses (exit 0 applied, 1 not running, 2 invalid, refused or error)",
        "       sindri workflow handback <address> [--nonce <n>] [--json]   hand a taken-over instance back: its timer resumes (exit 0 applied, 1 not running, 2 invalid, refused or error)",
        "       sindri handback <item-or-address> [--nonce <n>] [--json]   short form of 'workflow handback' (a bare item is its latest attempt)",
        "       sindri workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--nonce <n>] [--json]   report an open desk run (exit 0 applied, 1 not running, 2 invalid, refused or error)",
    ];

    internal static readonly string Text = string.Join('\n', [.. _verbs, "exit codes: 0 ok, 1 attention needed, 2 error"]);
}
