using System.Text.Json.Nodes;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>One GraphQL request: the operation text and its variables.</summary>
/// <param name="Query">The Query Linear serves.</param>
/// <param name="Variables">The Variables Linear serves.</param>
internal sealed record LinearRequest(string Query, JsonObject? Variables);
