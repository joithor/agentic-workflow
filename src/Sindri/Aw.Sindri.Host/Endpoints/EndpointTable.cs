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
        var queries = group.MapGroup(string.Empty);
        var commands = group.MapGroup(string.Empty).WithMetadata(CommandRouteMetadata.Instance);   // Origin refused before binding (SND-API-008)

        // ---- queries: one line per slice, sorted ----
        _ = queries.MapExportContextMap();
        _ = queries.MapExportContextReview();
        _ = queries.MapFindContextPath();
        _ = queries.MapGetContextFileMap();
        _ = queries.MapGetContextLenses();
        _ = queries.MapGetContextMap();
        _ = queries.MapGetContextProposals();
        _ = queries.MapGetContextReview();
        _ = queries.MapGetContextReviewHistory();
        _ = queries.MapGetContextSources();
        _ = queries.MapGetStatus();
        _ = queries.MapListContextNeighbors();
        _ = queries.MapListCommands();
        _ = queries.MapListRecipes();
        _ = queries.MapPingProcess();
        _ = queries.MapQueryContext();
        _ = queries.MapShowContextNode();
        _ = queries.MapShowRecipe();
        _ = queries.MapStreamEvents();

        // ---- commands: one line per slice, sorted ----
        _ = commands.MapApproveProfile();
        _ = commands.MapImportContextMap();
        _ = commands.MapPromoteContextMap();
        _ = commands.MapProposeContextMap();
        _ = commands.MapReviewContextMap();
        _ = commands.MapStopProcess();
        _ = commands.MapApproveRecipe();
        _ = commands.MapRevokeRecipe();
        _ = commands.MapIssueChallenge();
        _ = commands.MapRegisterRecipe();
        return app;
    }
}
