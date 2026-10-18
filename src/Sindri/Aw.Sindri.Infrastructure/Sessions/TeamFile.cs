namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The two fields of a Meta team.json that host detection reads; every other member is ignored.</summary>
internal sealed record TeamFile(string? Team, IReadOnlyList<string>? Lanes);
