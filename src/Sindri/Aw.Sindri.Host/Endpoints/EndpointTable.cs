namespace Aw.Sindri.Host.Endpoints;

/// <summary>Maps every Sindri endpoint under /sindri; one Map&lt;Slice&gt;() call per slice.</summary>
internal static class EndpointTable
{
    internal static WebApplication MapSindriEndpoints(this WebApplication app)
    {
        _ = app.MapGroup("/sindri").MapGetStatus();
        return app;
    }
}
