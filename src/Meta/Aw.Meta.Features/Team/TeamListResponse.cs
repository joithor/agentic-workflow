using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Team;

/// <summary>The TeamList response.</summary>
/// <param name="Teams">Every team found, by name.</param>
public sealed record TeamListResponse(IReadOnlyList<TeamSummary> Teams);
