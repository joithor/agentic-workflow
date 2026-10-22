namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the IssueHits operation.</summary>
/// <param name="Issues">The Issues Linear serves.</param>
internal sealed record LinearIssuesData(LinearConnection<LinearIssue> Issues);
