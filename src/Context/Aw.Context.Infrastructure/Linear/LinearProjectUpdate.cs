namespace Aw.Context.Infrastructure.Linear;

/// <summary>A project update.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Body">The Body Linear serves.</param>
/// <param name="UpdatedAt">The UpdatedAt Linear serves.</param>
/// <param name="CreatedAt">The CreatedAt Linear serves.</param>
/// <param name="Health">The Health Linear serves.</param>
/// <param name="Url">The Url Linear serves.</param>
/// <param name="User">The User Linear serves.</param>
/// <param name="Project">The Project Linear serves.</param>
internal sealed record LinearProjectUpdate(string Id, string Body, DateTimeOffset UpdatedAt, DateTimeOffset CreatedAt, string? Health = null, string? Url = null, LinearNamed? User = null, LinearNamed? Project = null);
