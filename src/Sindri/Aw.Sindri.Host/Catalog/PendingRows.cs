using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ProbeLaunchTargets;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The rows of API P3–P10a's actions whose phase has not flipped them yet, so the app and doctor can list them (P1 plan G7): UI status
/// 'pending:&lt;phase&gt;'. A row with no command type is registered before its phase ships; one with a type is built and served, and waits only for its
/// catalog flip to 'ready' (API P10a unit 13, which re-records the catalog golden the UI reads).
/// </summary>
internal static class PendingRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("ProbeLaunchTargets"), typeof(ProbeLaunchTargetsRequest), Variant: null, ActionClass.Job, ServiceGrants: [], NeedsTerminal: false, SelfAudited: false,
            Destructive: false, ReadsViews: false, Shortcut: null, "POST", "/sindri/launch/targets/probe", ShellRows.Form("sindri launch probe"), "ready"),
        Pending("FocusSession", ActionClass.Desk, "POST /sindri/sessions/{provider}/{id}/focus", "sindri agents focus <ref>", "CO-4a"),   // collector spec CA-4, CA-7: CO-4a binds its type
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
