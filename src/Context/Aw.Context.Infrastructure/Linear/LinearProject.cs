namespace Aw.Context.Infrastructure.Linear;

/// <summary>A Linear project.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="UpdatedAt">The UpdatedAt Linear serves.</param>
/// <param name="Description">The Description Linear serves.</param>
/// <param name="Url">The Url Linear serves.</param>
/// <param name="State">The State Linear serves.</param>
/// <param name="CreatedAt">The CreatedAt Linear serves.</param>
/// <param name="StartDate">The StartDate Linear serves.</param>
/// <param name="TargetDate">The TargetDate Linear serves.</param>
/// <param name="Lead">The Lead Linear serves.</param>
/// <param name="Teams">The Teams Linear serves.</param>
internal sealed record LinearProject(string Id, string Name, DateTimeOffset UpdatedAt, string? Description = null, string? Url = null, string? State = null, DateTimeOffset? CreatedAt = null, string? StartDate = null, string? TargetDate = null, LinearNamed? Lead = null, LinearConnection<LinearTeam>? Teams = null);
