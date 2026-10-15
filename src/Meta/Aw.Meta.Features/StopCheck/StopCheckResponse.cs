using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Features.StopCheck;

/// <summary>The StopCheck response.</summary>
/// <param name="IsMeta">Whether the session is the registered meta session; any other session may always stop.</param>
/// <param name="Blocked">Whether the stop is blocked.</param>
/// <param name="Open">The open obligations, each with the exact next command.</param>
/// <param name="Warning">A one-line warning when the stop is let through with obligations open; null otherwise.</param>
public sealed record StopCheckResponse(bool IsMeta, bool Blocked, IReadOnlyList<Obligation> Open, string? Warning);
