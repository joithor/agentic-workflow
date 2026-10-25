namespace Aw.Context.Infrastructure.Linear;

/// <summary>A relation between two issues (blocks, duplicate, related, ...). In an issue's relations the other issue is RelatedIssue; in its inverse relations it is Issue.</summary>
/// <param name="Type">The Type Linear serves.</param>
/// <param name="RelatedIssue">The RelatedIssue Linear serves.</param>
/// <param name="Issue">The Issue Linear serves.</param>
internal sealed record LinearRelation(string Type, LinearIssueRef? RelatedIssue = null, LinearIssueRef? Issue = null);
