namespace Aw.Context.Infrastructure.Linear;

/// <summary>An issue named by a relation: its identifier and title.</summary>
/// <param name="Identifier">The Identifier Linear serves.</param>
/// <param name="Title">The Title Linear serves.</param>
internal sealed record LinearIssueRef(string Identifier, string? Title = null);
