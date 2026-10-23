namespace Aw.Context.Infrastructure.Linear;

/// <summary>Linear's GraphQL envelope: the data, and the errors if any.</summary>
/// <typeparam name="T">The node or data type.</typeparam>
/// <param name="Data">The Data Linear serves.</param>
/// <param name="Errors">The Errors Linear serves.</param>
internal sealed record LinearReply<T>(T? Data = default, IReadOnlyList<LinearGraphQlError>? Errors = null);
