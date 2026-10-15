namespace Aw.Context.Infrastructure.Onyx;

/// <summary>A method and route the onyx adapter may send (<see cref="OnyxRequests.Allowlist"/>).</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Route">The route under Onyx's base URL.</param>
internal sealed record OnyxRoute(HttpMethod Method, string Route);
