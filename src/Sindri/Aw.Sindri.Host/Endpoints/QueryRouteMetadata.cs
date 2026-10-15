namespace Aw.Sindri.Host.Endpoints;

/// <summary>Marks a query endpoint, so read-your-writes (X-Sindri-After) applies to it (API spec §2.1, §2.4).</summary>
internal sealed class QueryRouteMetadata
{
    private QueryRouteMetadata()
    {
    }

    /// <summary>The one instance every query route group carries.</summary>
    internal static QueryRouteMetadata Instance { get; } = new();
}
