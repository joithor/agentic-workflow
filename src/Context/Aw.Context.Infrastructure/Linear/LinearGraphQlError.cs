namespace Aw.Context.Infrastructure.Linear;

/// <summary>One GraphQL error.</summary>
/// <param name="Message">The Message Linear serves.</param>
/// <param name="Extensions">The Extensions Linear serves.</param>
internal sealed record LinearGraphQlError(string Message, LinearErrorExtensions? Extensions = null);
