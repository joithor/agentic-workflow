using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The context engine's command rows (API spec §3.7; context spec §8.5). Its GETs are queries and have no rows.
/// SyncSources is a job (API §4.1), which needs API P6's job runtime, so it is registered 'pending:S-5B' (the context stage
/// that adds the job route once P6 merges, not P6's own sweep) with no route mapped yet; until then the
/// 'context-docs-refresh' scheduled service refreshes repo-docs, and S-5B makes the row ready over the same refresh.
/// </summary>
internal static class ContextRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("ImportContextMap"), typeof(ImportContextMapRequest), Variant: null, ActionClass.Protected, ServiceGrants: [],
            NeedsTerminal: true, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/import", ShellRows.Form("sindri context map import <file>"), "cli-only:typed-confirmation"),
        new(ActionId.From("SyncSources"), CommandType: null, Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/sources/sync", ShellRows.Form("sindri context sources sync"), "pending:S-5B"),
    ];
}
