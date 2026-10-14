namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Maps every Sindri endpoint under /sindri (factory addendum §17.21 API): each binds, calls one service and maps its Result.
/// Queries are GETs and commands POSTs (ARCH-CQRS-001); each slice adds one line under its group's anchor (API spec C7).
/// </summary>
internal static class EndpointTable
{
    internal static WebApplication MapSindriEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sindri");
        var queries = group.MapGroup(string.Empty).WithMetadata(QueryRouteMetadata.Instance);   // X-Sindri-After honoured (read-your-writes)
        var commands = group.MapGroup(string.Empty).WithMetadata(CommandRouteMetadata.Instance);   // Origin refused before binding (SND-API-008)

        // ---- queries: one line per slice, sorted ----
        _ = queries.MapGetItem();
        _ = queries.MapGetItemTimeline();
        _ = queries.MapGetProfile();
        _ = queries.MapGetQueue();
        _ = queries.MapExportContextMap();
        _ = queries.MapGetContextMap();
        _ = queries.MapGetContextSources();
        _ = queries.MapGetStatus();
        _ = queries.MapListCommands();
        _ = queries.MapListItems();
        _ = queries.MapListServices();
        _ = queries.MapPingProcess();
        _ = queries.MapQueryContext();
        _ = queries.MapStreamEvents();

        // ---- commands: one line per slice, sorted ----
        _ = commands.MapApproveProfile();
        _ = commands.MapCloseItem();
        _ = commands.MapCreateItem();
        _ = commands.MapRebuildViews();
        _ = commands.MapImportContextMap();
        _ = commands.MapStopProcess();
        return app;
    }
}
