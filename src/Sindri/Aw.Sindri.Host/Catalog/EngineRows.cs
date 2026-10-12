using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.RevokeRecipe;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The workflow engine's rows (engine spec §6.2, §6.3 route table; UI spec §7, E-9, E-12; API spec §2.1, §4.1). P2's
/// recipe commands are bound to their types and flip to 'ready' as their units ship them; protected rows end as
/// 'cli-only:typed-confirmation' once mapped. The instance rows wait for WF-P4 and WF-P5 with no type yet, like PendingRows.
/// Queries (show, list, get, events) have no rows: the catalog lists commands, variants and issues only (API spec §3.3).
/// </summary>
internal static class EngineRows
{
    /// <summary>The UI status a protected row takes once its endpoint is mapped.</summary>
    internal const string TypedConfirmation = "cli-only:typed-confirmation";
    private const string Commands = "POST /sindri/workflow/instances/{instanceId}/commands";

    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        // ---- P2: the registry (bound) ----
        Bound("RegisterRecipe", typeof(RegisterRecipeRequest), ActionClass.Desk, "POST /sindri/workflow/recipes", "sindri recipe register <file>"),
        Protected("ApproveRecipe", "POST /sindri/workflow/recipes/{id}@{hash}/approve", "sindri recipe approve <recipe>", "WF-P2") with { CommandType = typeof(ApproveRecipeRequest), Ui = TypedConfirmation },
        Protected("RevokeRecipe", "POST /sindri/workflow/recipes/{id}@{hash}/revoke", "sindri recipe revoke <recipe>", "WF-P2") with { CommandType = typeof(RevokeRecipeRequest), Ui = TypedConfirmation },
        Protected("IssueChallenge", "POST /sindri/workflow/challenges", "sindri recipe approve <recipe>", "WF-P2") with { CommandType = typeof(IssueChallengeRequest), Class = ActionClass.Issue, Ui = "ready" },

        // ---- P4 and P5: instances (pending, no type yet) ----
        Pending("StartInstance", ActionClass.Desk, "POST /sindri/workflow/instances", "sindri workflow start <recipe> --item <itemId>", "WF-P4"),
        Pending("IssueNonce", ActionClass.Issue, "POST /sindri/workflow/instances/{instanceId}/nonce", "sindri workflow command <address> <verb>", "WF-P4"),
        Pending("ApplyCommand:answer-text", ActionClass.Desk, Commands, "sindri workflow answer <address> --text <text>", "WF-P4"),
        Pending("ApplyCommand:answer-resume", ActionClass.Desk, Commands, "sindri workflow answer <address> --resume", "WF-P4"),
        Pending("ApplyCommand:answer-reject", ActionClass.Desk, Commands, "sindri workflow answer <address> --reject", "WF-P4") with { Destructive = true },
        Protected("ApplyCommand:answer-grant", Commands, "sindri workflow answer <address> --grant <rounds>", "WF-P4"),
        Protected("ApplyCommand:answer-grant-tokens", Commands, "sindri workflow answer <address> --grant-tokens <tokens>", "WF-P4"),
        Protected("ApplyCommand:answer-accept", Commands, "sindri workflow answer <address> --accept", "WF-P4"),
        Pending("ApplyCommand:takeover", ActionClass.Pause, Commands, "sindri workflow takeover <address>", "WF-P5"),
        Pending("ApplyCommand:handback", ActionClass.Desk, Commands, "sindri workflow handback <address>", "WF-P5"),
        Pending("ApplyCommand:cancel", ActionClass.Desk, Commands, "sindri workflow cancel <address>", "WF-P4") with { Destructive = true },
        Pending("CompleteStep", ActionClass.Desk, "POST /sindri/workflow/instances/{instanceId}/runs/{run}/complete",
            "sindri workflow complete <address> --run <token> --outcome <outcome>", "WF-P4"),
    ];

    /// <summary>A P2 row bound to its type, pending until its unit ships its endpoint.</summary>
    private static CommandRow Bound(string action, Type type, ActionClass actionClass, string route, string cli) =>
        Pending(action, actionClass, route, cli, "WF-P2") with { CommandType = type };

    /// <summary>
    /// A protected row: terminal-bound. It stays 'pending:&lt;phase&gt;' until its endpoint is mapped (every non-pending row's
    /// route must be, EndpointCqrsTests), then the unit that maps it sets <see cref="TypedConfirmation"/> (the app shows the CLI to copy).
    /// </summary>
    private static CommandRow Protected(string action, string route, string cli, string phase) =>
        Pending(action, ActionClass.Protected, route, cli, phase) with { NeedsTerminal = true };

    /// <summary>A pending row: '&lt;Command&gt;[:&lt;variant&gt;]', its class, 'METHOD /route', its CLI form and the phase that ships it; no type yet.</summary>
    private static CommandRow Pending(string action, ActionClass actionClass, string route, string cli, string phase)
    {
        var colon = action.IndexOf(':', StringComparison.Ordinal);
        var space = route.IndexOf(' ', StringComparison.Ordinal);
        return new CommandRow(ActionId.From(action), CommandType: null, colon < 0 ? null : action[(colon + 1)..], actionClass,
            ServiceGrants: [], NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false,
            Shortcut: null, route[..space], route[(space + 1)..], ShellRows.Form(cli), "pending:" + phase);
    }
}
