namespace Aw.Context.Infrastructure.Linear;

/// <summary>An id and a name (a user, a project reference).</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
internal sealed record LinearNamed(string Id, string? Name = null);
