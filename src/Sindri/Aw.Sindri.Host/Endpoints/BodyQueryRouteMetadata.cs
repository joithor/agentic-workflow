namespace Aw.Sindri.Host.Endpoints;

/// <summary>Marks a body query (an HTTP QUERY route), so CommandOriginRefusal refuses an Origin header on it before its body is bound, as it does a command; the route is still a query (read-your-writes, no ledger event).</summary>
internal sealed class BodyQueryRouteMetadata
{
    private BodyQueryRouteMetadata()
    {
    }

    /// <summary>The one instance every body-query route carries.</summary>
    internal static BodyQueryRouteMetadata Instance { get; } = new();
}
