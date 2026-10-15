namespace Aw.Sindri.Host.Mcp;

/// <summary>Marks the routes MapMcp adds, so the MCP middleware finds them the way routing does (any casing, a trailing slash).</summary>
internal sealed class McpRouteMetadata
{
    /// <summary>The one instance.</summary>
    internal static McpRouteMetadata Instance { get; } = new();

    private McpRouteMetadata()
    {
    }
}
