using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The context engine's command rows (API spec §3.7; context spec §8.5). Its GETs are queries and have no rows.
/// SyncSources is a job (API §4.1), which needs API P6's job runtime, so it is registered pending:P6 with no route
/// mapped yet; until then the 'context-docs-refresh' scheduled service refreshes repo-docs, and P6 makes the row ready
/// over the same refresh.
/// </summary>
internal static class ContextRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("SyncSources"), CommandType: null, Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/sources/sync", ShellRows.Form("sindri context sources sync"), "pending:P6"),
    ];
}
