namespace Aw.Context.Infrastructure.Linear;

/// <summary>A Linear issue.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Identifier">The Identifier Linear serves.</param>
/// <param name="Title">The Title Linear serves.</param>
/// <param name="UpdatedAt">The UpdatedAt Linear serves.</param>
/// <param name="Description">The Description Linear serves.</param>
/// <param name="Url">The Url Linear serves.</param>
/// <param name="CreatedAt">The CreatedAt Linear serves.</param>
/// <param name="Team">The Team Linear serves.</param>
/// <param name="Creator">The Creator Linear serves.</param>
/// <param name="Comments">The Comments Linear serves.</param>
/// <param name="Attachments">The Attachments Linear serves.</param>
internal sealed record LinearIssue(string Id, string Identifier, string Title, DateTimeOffset UpdatedAt, string? Description = null, string? Url = null, DateTimeOffset? CreatedAt = null, LinearTeam? Team = null, LinearNamed? Creator = null, LinearConnection<LinearComment>? Comments = null, LinearConnection<LinearAttachment>? Attachments = null);
