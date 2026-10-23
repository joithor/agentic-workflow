namespace Aw.Context.Infrastructure.Linear;

/// <summary>A GraphQL connection: its nodes and paging.</summary>
/// <typeparam name="T">The node or data type.</typeparam>
/// <param name="Nodes">The Nodes Linear serves.</param>
/// <param name="PageInfo">The PageInfo Linear serves.</param>
internal sealed record LinearConnection<T>(IReadOnlyList<T> Nodes, LinearPageInfo? PageInfo = null);
