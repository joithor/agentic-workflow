using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The row helper for job-class commands (API P6): one line per job in a lane's rows, next to its AddJob and MapJob lines.</summary>
internal static class JobRows
{
    /// <summary>A ready job row: class 'job', no view wait, no shortcut.</summary>
    /// <param name="action">The command's name ('SyncSources'); it is the job's kind.</param>
    /// <param name="request">The request record, which implements IJob.</param>
    /// <param name="route">The full route, as the other rows write it, for example '/sindri/context/sources/sync' (MapJob takes it without '/sindri').</param>
    /// <param name="cli">The CLI form, for example 'sindri context sources sync'.</param>
    /// <returns>The row.</returns>
    internal static CommandRow Job(string action, Type request, string route, string cli) =>
        new(ActionId.From(action), request, Variant: null, ActionClass.Job, ServiceGrants: [], NeedsTerminal: false, SelfAudited: false,
            Destructive: false, ReadsViews: false, Shortcut: null, "POST", route, ShellRows.Form(cli), "ready");
}
