namespace Aw.Context.Infrastructure.Linear;

/// <summary>A Linear Document.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Title">The Title Linear serves.</param>
/// <param name="UpdatedAt">The UpdatedAt Linear serves.</param>
/// <param name="Content">The Content Linear serves.</param>
/// <param name="Url">The Url Linear serves.</param>
/// <param name="CreatedAt">The CreatedAt Linear serves.</param>
/// <param name="Creator">The Creator Linear serves.</param>
/// <param name="Project">The Project Linear serves.</param>
internal sealed record LinearDocument(string Id, string Title, DateTimeOffset UpdatedAt, string? Content = null, string? Url = null, DateTimeOffset? CreatedAt = null, LinearNamed? Creator = null, LinearNamed? Project = null);
