namespace Aw.Context.Infrastructure.Linear;

/// <summary>An issue comment.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Body">The Body Linear serves.</param>
/// <param name="CreatedAt">The CreatedAt Linear serves.</param>
/// <param name="User">The User Linear serves.</param>
internal sealed record LinearComment(string Id, string Body, DateTimeOffset CreatedAt, LinearNamed? User = null);
