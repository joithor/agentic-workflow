using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ListCommands;

/// <summary>One catalog row as served (API spec §3.1, UI spec E-8).</summary>
/// <param name="Action">The action id: '&lt;Command&gt;' or '&lt;Command&gt;:&lt;variant&gt;'.</param>
/// <param name="Class">The action class.</param>
/// <param name="AllowedCallers">Who may run it: 'human+terminal', 'human', 'session', 'service:&lt;name&gt;'.</param>
/// <param name="Terminal">True when it needs a human at a terminal.</param>
/// <param name="Method">The HTTP method, or null for a local verb.</param>
/// <param name="Route">The route, or null for a local verb.</param>
/// <param name="Cli">The CLI form.</param>
/// <param name="Ui">The UI status: 'ready', 'pending:&lt;phase&gt;' or 'cli-only:&lt;reason&gt;'.</param>
/// <param name="Destructive">True when the UI confirms it first.</param>
/// <param name="ReadsViews">True when it waits for the views (disabled during a rebuild).</param>
/// <param name="Shortcut">Its default key, or null.</param>
/// <param name="Confirm">How the app confirms it ('typed' or 'held'), or null when the app does not confirm it (setup-once spec §7.4).</param>
public sealed record CommandListRow(ActionId Action, ActionClass Class, IReadOnlyList<string> AllowedCallers, bool Terminal,
    string? Method, string? Route, string Cli, string Ui, bool Destructive, bool ReadsViews, string? Shortcut, string? Confirm = null);
