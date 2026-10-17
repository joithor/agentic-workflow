using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The rows of API P3–P10a's actions, registered before their phase ships so the app and doctor can list them (P1 plan
/// G7): no command type yet, UI status 'pending:&lt;phase&gt;'. Each phase binds its type and flips its rows to 'ready'.
/// Class, route and CLI come from API spec §3.2 and UI spec §7 (E-1, E-3, E-8); engine and context rows stay with their lanes.
/// </summary>
internal static class PendingRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Pending("ProbeLaunchTargets", ActionClass.Job, "POST /sindri/launch/targets/probe", "sindri launch probe", "P10a"),
    ];

    /// <summary>A pending row: '&lt;Command&gt;[:&lt;variant&gt;]', its class, 'METHOD /route', its CLI form and the phase that ships it.</summary>
    private static CommandRow Pending(string action, ActionClass actionClass, string route, string cli, string phase)
    {
        var colon = action.IndexOf(':', StringComparison.Ordinal);
        var space = route.IndexOf(' ', StringComparison.Ordinal);
        return new CommandRow(ActionId.From(action), CommandType: null, colon < 0 ? null : action[(colon + 1)..], actionClass,
            ServiceGrants: [], NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            route[..space], route[(space + 1)..], ShellRows.Form(cli), "pending:" + phase);
    }
}
