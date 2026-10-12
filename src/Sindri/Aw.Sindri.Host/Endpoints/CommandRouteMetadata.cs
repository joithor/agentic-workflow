namespace Aw.Sindri.Host.Endpoints;

/// <summary>Marks a command (or issue) endpoint, so CommandOriginRefusal refuses an Origin header on it before its body is bound (plan G21).</summary>
internal sealed class CommandRouteMetadata
{
    private CommandRouteMetadata()
    {
    }

    /// <summary>The one instance every command route group carries.</summary>
    internal static CommandRouteMetadata Instance { get; } = new();
}
