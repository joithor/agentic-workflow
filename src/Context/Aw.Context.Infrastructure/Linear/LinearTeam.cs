namespace Aw.Context.Infrastructure.Linear;

/// <summary>A team.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Key">The Key Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
internal sealed record LinearTeam(string Id, string Key, string? Name = null);
