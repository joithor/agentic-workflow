using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Features.OnyxEnsure;

/// <summary>What serve found and did about the managed Onyx.</summary>
/// <param name="Endpoint">What control.json records beside Temporal, or null when the profile names no managed Onyx.</param>
/// <param name="Detail">Why it is not running (no secret), or null.</param>
public sealed record OnyxEnsureResponse(OnyxEndpoint? Endpoint, string? Detail);
